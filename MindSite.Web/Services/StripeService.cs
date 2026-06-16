using Stripe;
using Stripe.Checkout;

namespace MindSite.Services
{
    public class StripeService
    {
        public StripeService(IConfiguration config)
        {
            StripeConfiguration.ApiKey = config["Stripe:SecretKey"];
        }

        public async Task<(string sessionId, string url)> CriarSessaoAsync(
            decimal valor,
            string descricao,
            string successUrl,
            string cancelUrl,
            int servicoId,
            string tipo)
        {
            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency    = "brl",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = descricao,
                            },
                            UnitAmount = (long)(valor * 100),
                        },
                        Quantity = 1,
                    }
                },
                Mode       = "payment",
                SuccessUrl = successUrl,
                CancelUrl  = cancelUrl,
                Metadata   = new Dictionary<string, string>
                {
                    ["servicoId"] = servicoId.ToString(),
                    ["tipo"]      = tipo,
                },
            };

            var service = new SessionService();
            var session = await service.CreateAsync(options);
            return (session.Id, session.Url);
        }

        // Reembolso parcial ou total de um PaymentIntent
        public async Task EmitirReembolsoAsync(string paymentIntentId, long valorCentavos)
        {
            var options = new RefundCreateOptions
            {
                PaymentIntent = paymentIntentId,
                Amount        = valorCentavos,
            };
            var service = new RefundService();
            await service.CreateAsync(options);
        }

        // Calcula a multa escalonada com base nos dias desde o pagamento
        // Retorna (percentualMulta, percentualReembolso)
        public static (decimal multa, decimal reembolso) CalcularMulta(DateTime pagamentoEm)
        {
            var dias = (DateTime.UtcNow - pagamentoEm).TotalDays;
            return dias switch
            {
                <= 3  => (0.10m, 0.90m),
                <= 15 => (0.25m, 0.75m),
                <= 30 => (0.50m, 0.50m),
                _     => (1.00m, 0.00m),
            };
        }
    }
}
