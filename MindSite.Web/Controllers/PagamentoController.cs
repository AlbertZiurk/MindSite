using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MindSite.Data;
using MindSite.Entities;
using MindSite.Enums;
using MindSite.Services;
using System.Security.Claims;
using Stripe;
using Stripe.Checkout;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MindSite.Controllers
{
    [Authorize]
    public class PagamentoController : Controller
    {
        private readonly AppDbContext _db;
        private readonly NotificacaoService _notif;
        private readonly IStripeClient _stripeClient;
        private readonly string _webhookSecret;

        private int UserId => int.Parse(User.FindFirstValue("UserId")!);

        public PagamentoController(AppDbContext db, NotificacaoService notif, IStripeClient stripeClient, IConfiguration config)
        {
            _db = db;
            _notif = notif;
            _stripeClient = stripeClient;
            // Puxa o whsec do ambiente/appsettings
            _webhookSecret = config["Stripe:WebhookSecret"] ?? "";
        }

        [Authorize(Roles = "Cliente")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> IniciarCheckout(int servicoId, string tipo, string metodoPagamento)
        {
            var servico = await _db.Servicos.FirstOrDefaultAsync(s => s.Id == servicoId && s.ClienteId == UserId);
            if (servico == null) return NotFound();

            // ... (Mantenha as suas validações de negócio intactas aqui)

            decimal percentual = tipo == "inicial" ? 0.30m : 0.70m;
            decimal valorDoPagamento = servico.Valor.Value * percentual;
            long valorEmCentavos = Convert.ToInt64(Math.Round(valorDoPagamento * 100));

            var paymentMethods = new List<string> { metodoPagamento == "pix" ? "pix" : "card" };

            var domain = $"{Request.Scheme}://{Request.Host}";
            var successUrl = $"{domain}/Pagamento/Sucesso?servicoId={servico.Id}";
            var cancelUrl = $"{domain}/Client/MeusServicos";

            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = paymentMethods,
                PaymentMethodOptions = metodoPagamento == "pix" ? new SessionPaymentMethodOptionsOptions
                {
                    Pix = new SessionPaymentMethodOptionsPixOptions
                    {
                        ExpiresAfterSeconds = 3600
                    }
                } : null,

                Metadata = new Dictionary<string, string>
                {
                    { "ServicoId", servico.Id.ToString() },
                    { "TipoPagamento", tipo }
                },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = valorEmCentavos,
                            Currency = "brl",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = $"Pagamento {tipo.ToUpper()} ({Convert.ToInt32(percentual * 100)}%) - Projeto #{servico.Id}"
                            },
                        },
                        Quantity = 1,
                    },
                },
                Mode = "payment",
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
            };

            var service = new SessionService(_stripeClient);
            Session session = await service.CreateAsync(options);

            if (tipo == "inicial") servico.StripeSessionIdInicial = session.Id;
            else servico.StripeSessionIdFinal = session.Id;

            await _db.SaveChangesAsync();
            return Redirect(session.Url);
        }

        [Authorize(Roles = "Cliente")]
        [HttpGet]
        public IActionResult Sucesso(int servicoId)
        {
            TempData["Success"] = "Pagamento recebido pelo gateway! O sistema está processando a transação. Aguarde a validação do administrador.";
            return RedirectToAction("MeusServicos", "Client");
        }

        [HttpPost("webhook/stripe")]
        [IgnoreAntiforgeryToken] 
        [AllowAnonymous] 
        public async Task<IActionResult> StripeWebhook()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            var signatureHeader = Request.Headers["Stripe-Signature"];

            try
            {
                var stripeEvent = EventUtility.ConstructEvent(json, signatureHeader, _webhookSecret);

                // CORREÇÃO: Utilizando estritamente a classe 'EventTypes' do SDK
                if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted ||
                    stripeEvent.Type == EventTypes.CheckoutSessionAsyncPaymentSucceeded)
                {
                    var session = stripeEvent.Data.Object as Stripe.Checkout.Session;

                    var servicoId = int.Parse(session.Metadata["ServicoId"]);
                    var tipo = session.Metadata["TipoPagamento"];

                    var servico = await _db.Servicos.FirstOrDefaultAsync(s => s.Id == servicoId);
                    if (servico != null)
                    {
                        if (tipo == "inicial" && !servico.PagamentoInicialFeito)
                        {
                            servico.PagamentoInicialEm = DateTime.UtcNow;
                            servico.StripePaymentIntentIdInicial = session.PaymentIntentId;
                        }
                        else if (tipo == "final" && !servico.PagamentoFinalFeito)
                        {
                            servico.PagamentoFinalEm = DateTime.UtcNow;
                            servico.StripePaymentIntentIdFinal = session.PaymentIntentId;
                        }

                        await _db.SaveChangesAsync();

                        // Dispara as notificações utilizando apenas a sua tabela 'Usuarios' mapeada no contexto
                        var admins = await _db.Usuarios.Where(u => u.Role == UserRole.Admin).ToListAsync();
                        foreach (var adm in admins)
                        {
                            await _notif.CriarAsync(adm.Id,
                                $"O pagamento {tipo} do Projeto #{servico.Id} foi registrado via Stripe. Valide no painel.",
                                url: "/Admin/Pedidos", servicoId: servico.Id);
                        }
                    }
                }
                return Ok();
            }
            catch (Exception)
            {
                return BadRequest();
            }
        }
    }
}