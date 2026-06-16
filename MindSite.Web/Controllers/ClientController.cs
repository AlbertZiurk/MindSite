using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MindSite.Data;
using MindSite.Hubs;
using MindSite.Entities;
using MindSite.Services;
using System.Security.Claims;
using System.Text.Json;
using MindSite.Enums;
using MindSite.ViewModels;
using AvaliarCompletoVM = MindSite.ViewModels.AvaliarCompletoViewModel;
using AvaliarSistemaVM  = MindSite.ViewModels.AvaliarSistemaViewModel;

namespace MindSite.Controllers
{
    [Authorize(Roles = "Cliente,Fornecedor")]
    public class ClientController : Controller
    {
        private readonly AppDbContext _db;
        private readonly NotificacaoService _notif;
        private readonly LogService _log;
        private readonly IHubContext<ChatHub> _hub;
        private readonly IWebHostEnvironment _env;

        public ClientController(AppDbContext db, NotificacaoService notif, LogService log, IHubContext<ChatHub> hub, IWebHostEnvironment env)
        {
            _db    = db;
            _notif = notif;
            _log   = log;
            _hub   = hub;
            _env   = env;
        }

        private int UserId => int.Parse(User.FindFirstValue("UserId")!);

        // Home do Cliente
        [Authorize(Roles = "Cliente")]
        public async Task<IActionResult> Index(bool form = false)
        {
            var usuario = await _db.Usuarios.FindAsync(UserId);
            ViewBag.NomeCompleto    = usuario?.NomeCompleto;
            ViewBag.CPF             = usuario?.CPF;
            ViewBag.AbrirFormulario = form;
            return View();
        }

        // Upload de arquivo de chat
        [HttpPost]
        public async Task<IActionResult> UploadChatArquivo(IFormFile arquivo)
        {
            if (arquivo == null || arquivo.Length == 0)
                return Json(new { ok = false, erro = "Arquivo inválido." });
            var ext = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
            if (!new[] { ".png", ".jpg", ".jpeg", ".pdf" }.Contains(ext))
                return Json(new { ok = false, erro = "Tipo não permitido. Use PNG, JPEG ou PDF." });
            if (arquivo.Length > 10 * 1024 * 1024)
                return Json(new { ok = false, erro = "Arquivo muito grande. Máximo 10 MB." });
            var uploadsPath = Path.Combine(_env.WebRootPath, "uploads", "chat");
            Directory.CreateDirectory(uploadsPath);
            var fileName = $"{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(uploadsPath, fileName);
            await using var stream = new FileStream(filePath, FileMode.Create);
            await arquivo.CopyToAsync(stream);
            return Json(new { ok = true, url = $"/uploads/chat/{fileName}", nome = arquivo.FileName });
        }

       // Chat
        public async Task<IActionResult> Chat(int? contatoId, bool grupoSuporte = false)
        {
            var uid = UserId;
            var eu  = await _db.Usuarios.FindAsync(uid);

            // Fornecedores pinados (sem admins — admins estão no grupo Suporte)
            var fornecedorIds = await _db.Servicos
                .Where(s => s.ClienteId == uid && s.FornecedorId.HasValue && s.PagamentoInicialFeito)
                .Select(s => s.FornecedorId!.Value)
                .Distinct()
                .ToListAsync();
            var fornecedores = await _db.Usuarios
                .Where(u => fornecedorIds.Contains(u.Id))
                .OrderBy(u => u.NomeCompleto)
                .ToListAsync();

            var contatosPinados = fornecedores.ToList();
            var pinadosIds      = contatosPinados.Select(c => c.Id).ToHashSet();

            // Outras conversas (sem admins individuais)
            var contatosIds = await _db.Mensagens
                .Where(m => m.RemetenteId == uid || m.DestinatarioId == uid)
                .Select(m => m.RemetenteId == uid ? m.DestinatarioId : m.RemetenteId)
                .Distinct()
                .ToListAsync();

            var conversas = new List<ConversaItem>();
            foreach (var cid in contatosIds.Where(id => !pinadosIds.Contains(id)))
            {
                var contato = await _db.Usuarios.FindAsync(cid);
                if (contato == null || contato.Role == UserRole.Admin) continue;
                var ult = await _db.Mensagens
                    .Where(m => (m.RemetenteId == uid && m.DestinatarioId == cid) ||
                                (m.RemetenteId == cid && m.DestinatarioId == uid))
                    .OrderByDescending(m => m.EnviadaEm).FirstOrDefaultAsync();
                var naoLidas = await _db.Mensagens
                    .CountAsync(m => m.RemetenteId == cid && m.DestinatarioId == uid && !m.Lida);
                conversas.Add(new ConversaItem
                {
                    Contato        = contato,
                    UltimaMensagem = ult?.Conteudo ?? "",
                    Quando         = ult?.EnviadaEm,
                    NaoLidas       = naoLidas
                });
            }

            foreach (var p in contatosPinados)
            {
                var ult = await _db.Mensagens
                    .Where(m => (m.RemetenteId == uid && m.DestinatarioId == p.Id) ||
                                (m.RemetenteId == p.Id && m.DestinatarioId == uid))
                    .OrderByDescending(m => m.EnviadaEm).FirstOrDefaultAsync();
                var naoLidas = await _db.Mensagens
                    .CountAsync(m => m.RemetenteId == p.Id && m.DestinatarioId == uid && !m.Lida);
                var servTipo = await _db.Servicos
                    .Where(s => s.ClienteId == uid && s.FornecedorId == p.Id && s.PagamentoInicialFeito)
                    .OrderByDescending(s => s.CriadoEm)
                    .Select(s => (TipoServico?)s.Tipo)
                    .FirstOrDefaultAsync();
                conversas.Insert(0, new ConversaItem
                {
                    Contato        = p,
                    UltimaMensagem = ult?.Conteudo ?? "",
                    Quando         = ult?.EnviadaEm,
                    NaoLidas       = naoLidas,
                    Pinado         = true,
                    TipoDoServico  = servTipo
                });
            }

            // Informações do grupo Suporte para a sidebar
            var grupoUlt = await _db.MensagensGrupo
                .Where(m => m.ThreadUserId == uid && !m.EhSistema)
                .OrderByDescending(m => m.EnviadaEm)
                .FirstOrDefaultAsync();

            List<MensagemGrupo> grupoMensagens = new();
            if (grupoSuporte)
            {
                grupoMensagens = await _db.MensagensGrupo
                    .Include(m => m.Remetente)
                    .Where(m => m.ThreadUserId == uid && !m.EhSistema)
                    .OrderBy(m => m.EnviadaEm)
                    .ToListAsync();
            }

            List<Mensagem> mensagens = new();
            Usuario? contatoSel = null;
            bool chatBloqueado = false;
            if (!grupoSuporte && contatoId.HasValue)
            {
                contatoSel = await _db.Usuarios.FindAsync(contatoId.Value);

                if (contatoSel?.Role == UserRole.Fornecedor)
                {
                    var temAcesso = await _db.Servicos.AnyAsync(s =>
                        s.ClienteId == uid &&
                        s.FornecedorId == contatoId.Value &&
                        s.PagamentoInicialFeito);
                    if (!temAcesso)
                        return RedirectToAction("Chat");

                    var statusAtivos = new List<ServicoStatus> { ServicoStatus.Ativo, ServicoStatus.MudancaSolicitada };
                    chatBloqueado = !await _db.Servicos.AnyAsync(s =>
                        s.ClienteId == uid &&
                        s.FornecedorId == contatoId.Value &&
                        statusAtivos.Contains(s.Status));
                }

                mensagens = await _db.Mensagens
                    .Include(m => m.Remetente)
                    .Where(m => (m.RemetenteId == uid && m.DestinatarioId == contatoId) ||
                                (m.RemetenteId == contatoId && m.DestinatarioId == uid))
                    .OrderBy(m => m.EnviadaEm).ToListAsync();

                foreach (var m in mensagens.Where(m => m.DestinatarioId == uid && !m.Lida)) m.Lida = true;
                await _db.SaveChangesAsync();
            }

            return View(new ChatViewModel
            {
                UsuarioAtual         = eu ?? new Usuario(),
                Conversas            = conversas.OrderByDescending(c => c.Pinado).ThenByDescending(c => c.Quando).ToList(),
                ContatoSelecionadoId = grupoSuporte ? null : contatoId,
                ContatoSelecionado   = contatoSel,
                Mensagens            = mensagens,
                ContatosPinados      = contatosPinados,
                ChatBloqueado        = chatBloqueado,
                IsGrupoSuporte       = grupoSuporte,
                GrupoMensagens       = grupoMensagens,
                GrupoUltimaMensagem  = grupoUlt?.Conteudo ?? "",
                GrupoUltimaData      = grupoUlt?.EnviadaEm
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EnviarMensagem(EnviarMensagemViewModel vm)
        {
            if (!ModelState.IsValid) return RedirectToAction("Chat", new { contatoId = vm.DestinatarioId });

            _db.Mensagens.Add(new Mensagem
            {
                RemetenteId    = UserId,
                DestinatarioId = vm.DestinatarioId,
                Conteudo       = vm.Conteudo.Trim()
            });
            await _db.SaveChangesAsync();

            var dest = await _db.Usuarios.FindAsync(vm.DestinatarioId);
            var chatUrl = dest?.Role switch {
                UserRole.Admin      => $"/Admin/Chat?contatoId={UserId}",
                UserRole.Fornecedor => $"/Supplier/Chat?contatoId={UserId}",
                _                   => $"/Client/Chat?contatoId={UserId}"
            };
            await _notif.CriarAsync(vm.DestinatarioId, "Mensagem recebida.", url: chatUrl);

            return RedirectToAction("Chat", new { contatoId = vm.DestinatarioId });
        }

        // Solicitar Serviço
        [Authorize(Roles = "Cliente"), HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SolicitarServico(SolicitarServicoViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                TempData["Erro"] = "Dados inválidos.";
                return RedirectToAction("Index");
            }

            if (string.IsNullOrWhiteSpace(vm.Requisitos) || vm.Requisitos.Trim().Length < 10)
            {
                TempData["Erro"] = "Preencha os requisitos do projeto antes de enviar.";
                return RedirectToAction("Index");
            }

            if (vm.PrazoCliente == PrazoTipo.DataEspecifica)
            {
                if (!vm.DataPrazoCliente.HasValue || vm.DataPrazoCliente.Value.Date <= DateTime.Today)
                {
                    TempData["Erro"] = "Selecione uma data futura para o prazo específico.";
                    return RedirectToAction("Index");
                }
            }

            var servico = new Servico
            {
                ClienteId        = UserId,
                Tipo             = vm.Tipo,
                NomeProjeto      = vm.NomeProjeto?.Trim(),
                Requisitos       = vm.Requisitos,
                PrazoCliente     = vm.PrazoCliente,
                DataPrazoCliente = vm.PrazoCliente == PrazoTipo.DataEspecifica ? vm.DataPrazoCliente : null,
                OrcamentoCliente = vm.OrcamentoCliente,
                Status           = ServicoStatus.Pendente
            };
            _db.Servicos.Add(servico);
            await _db.SaveChangesAsync();
            await _log.RegistrarAsync(UserId, $"Solicitou serviço de {vm.Tipo}.");

            // Notifica apenas admins para revisar e enviar para fornecedores
            var admins = await _db.Usuarios.Where(u => u.Role == UserRole.Admin).ToListAsync();
            foreach (var adm in admins)
                await _notif.CriarAsync(adm.Id,
                    "Novo pedido recebido.",
                    url: "/Admin/Pedidos", servicoId: servico.Id);

            TempData["Success"] = "Projeto solicitado! Aguardando um fornecedor aceitar.";
            return RedirectToAction("MeusServicos");
        }

        // Meus Serviços
        [Authorize(Roles = "Cliente")]
        public async Task<IActionResult> MeusServicos()
        {
            var uid = UserId;
            var servicos = await _db.Servicos
                .Include(s => s.Fornecedor)
                .Include(s => s.Mudancas)
                .Include(s => s.Propostas).ThenInclude(p => p.Fornecedor)
                .Include(s => s.Arquivos)
                .Include(s => s.Avaliacao)
                .Include(s => s.AvaliacoesSistema)
                .Where(s => s.ClienteId == uid)
                .OrderByDescending(s => s.CriadoEm)
                .ToListAsync();
            ViewBag.UserId = uid;
            return View(servicos);
        }

        // Concluir Serviço
        [Authorize(Roles = "Cliente"), HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ConcluirServico(int id)
        {
            var servico = await _db.Servicos
                .FirstOrDefaultAsync(s => s.Id == id && s.ClienteId == UserId);
            if (servico == null) return NotFound();

            servico.Status       = ServicoStatus.AguardandoPagamentoFinal;
            servico.AtualizadoEm = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            if (servico.FornecedorId.HasValue)
                await _notif.CriarAsync(servico.FornecedorId.Value,
                    "Entrega confirmada.",
                    servicoId: id);

            var admins = await _db.Usuarios.Where(u => u.Role == UserRole.Admin).ToListAsync();
            foreach (var adm in admins)
                await _notif.CriarAsync(adm.Id,
                    "Entrega confirmada.",
                    url: "/Admin/Pedidos", servicoId: id);

            await _log.RegistrarAsync(UserId, $"Confirmou entrega do serviço #{id}.");

            TempData["Success"] = "Entrega confirmada! Aguardando confirmação do pagamento final pelo admin.";
            return RedirectToAction("MeusServicos");
        }

        // Escolher Fornecedor
        [Authorize(Roles = "Cliente"), HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EscolherFornecedor(int servicoId, int propostaId)
        {
            var servico = await _db.Servicos
                .Include(s => s.Propostas)
                .FirstOrDefaultAsync(s => s.Id == servicoId && s.ClienteId == UserId);
            if (servico == null) return NotFound();
            if (servico.Status != ServicoStatus.AguardandoEscolha)
            {
                TempData["Erro"] = "Este projeto não está aguardando escolha de fornecedor.";
                return RedirectToAction("MeusServicos");
            }

            var proposta = servico.Propostas.FirstOrDefault(p => p.Id == propostaId);
            if (proposta == null) return NotFound();

            foreach (var p in servico.Propostas)
                p.Status = p.Id == propostaId ? PropostaStatus.Aceita : PropostaStatus.Recusada;

            servico.FornecedorId = proposta.FornecedorId;
            servico.Valor        = proposta.Valor;
            servico.DataEntrega  = proposta.DataEntrega;
            servico.Status       = ServicoStatus.Ativo;
            servico.AtualizadoEm = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await _notif.CriarAsync(proposta.FornecedorId,
                "Proposta aceita!",
                url: "/Supplier/Servicos?filtro=meus", servicoId: servicoId);

            foreach (var p in servico.Propostas.Where(p => p.Id != propostaId))
                await _notif.CriarAsync(p.FornecedorId,
                    "Proposta não selecionada.",
                    url: "/Supplier/Servicos", servicoId: servicoId);

            var admins = await _db.Usuarios.Where(u => u.Role == UserRole.Admin).ToListAsync();
            foreach (var adm in admins)
                await _notif.CriarAsync(adm.Id,
                    "Fornecedor selecionado.",
                    url: "/Admin/Pedidos", servicoId: servicoId);

            await _log.RegistrarAsync(UserId, $"Escolheu fornecedor para serviço #{servicoId}.");

            TempData["Success"] = "Fornecedor escolhido! Aguarde a confirmação do pagamento inicial pelo admin.";
            return RedirectToAction("MeusServicos");
        }

        // Cancelar Serviço
        [Authorize(Roles = "Cliente"), HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelarServico(int id)
        {
            var servico = await _db.Servicos
                .FirstOrDefaultAsync(s => s.Id == id && s.ClienteId == UserId);
            if (servico == null) return NotFound();
            if (servico.Status == ServicoStatus.Concluido || servico.Status == ServicoStatus.Cancelado)
            {
                TempData["Erro"] = "Este projeto não pode ser cancelado.";
                return RedirectToAction("MeusServicos");
            }

            // Calcula reembolso Pix (processado manualmente pela equipe)
            string infoReembolso = "";
            if (servico.PagamentoInicialFeito && servico.PagamentoInicialEm.HasValue)
            {
                var valorPago = servico.Valor.GetValueOrDefault() * 0.3m;
                var (percMulta, percReembolso) = StripeService.CalcularMulta(servico.PagamentoInicialEm.Value);
                var valorReembolso = Math.Round(valorPago * percReembolso, 2);
                var valorMulta     = Math.Round(valorPago * percMulta, 2);

                infoReembolso = valorReembolso > 0
                    ? $" Reembolso de R$ {valorReembolso:N2} será processado pela equipe MindSite em até 5 dias úteis. Multa aplicada: R$ {valorMulta:N2}."
                    : " Cancelamento após 30 dias do pagamento inicial: nenhum reembolso aplicável.";
            }

            // Remove todas as notificações vinculadas a este serviço
            var notifParaRemover = await _db.Notificacoes
                .Where(n => n.ServicoId == id)
                .ToListAsync();
            _db.Notificacoes.RemoveRange(notifParaRemover);

            // Apaga a conversa com o fornecedor, se houver
            var fornecedorIdCancelado = servico.FornecedorId;
            if (fornecedorIdCancelado.HasValue)
            {
                var mensagensParaRemover = await _db.Mensagens
                    .Where(m => (m.RemetenteId == UserId && m.DestinatarioId == fornecedorIdCancelado.Value) ||
                                (m.RemetenteId == fornecedorIdCancelado.Value && m.DestinatarioId == UserId))
                    .ToListAsync();
                _db.Mensagens.RemoveRange(mensagensParaRemover);
            }

            servico.Status       = ServicoStatus.Cancelado;
            servico.AtualizadoEm = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // Corta o contato em tempo real via SignalR
            if (fornecedorIdCancelado.HasValue)
            {
                await _hub.Clients.Group($"user-{UserId}").SendAsync("ConversationCleared", new { contatoId = fornecedorIdCancelado.Value });
                await _hub.Clients.Group($"user-{fornecedorIdCancelado.Value}").SendAsync("ConversationCleared", new { contatoId = UserId });
            }

            await _log.RegistrarAsync(UserId, $"Cancelou serviço #{id}.");

            TempData["Info"] = $"Projeto cancelado.{infoReembolso}";
            return RedirectToAction("MeusServicos");
        }

        // Solicitar Mudança (via MeusServicos — mantido por compatibilidade)
        [Authorize(Roles = "Cliente"), HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SolicitarMudanca(int servicoId, string descricao)
        {
            var servico = await _db.Servicos.FirstOrDefaultAsync(s => s.Id == servicoId && s.ClienteId == UserId);
            if (servico == null) return NotFound();

            var campos = new[] { new { campo = "Descrição", de = (string?)null, para = descricao.Trim() } };
            var descJson = JsonSerializer.Serialize(campos);

            var mudanca = new SolicitacaoMudanca
            {
                ServicoId     = servicoId,
                SolicitanteId = UserId,
                Descricao     = descJson
            };
            _db.SolicitacoesMudanca.Add(mudanca);
            servico.Status = ServicoStatus.MudancaSolicitada;
            servico.AtualizadoEm = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            if (servico.FornecedorId.HasValue)
            {
                var eu = await _db.Usuarios.FindAsync(UserId);
                var resumo = descricao.Length > 60 ? descricao[..60] + "..." : descricao;
                var msgContent = JsonSerializer.Serialize(new { _t = "M", id = mudanca.Id, r = $"Solicitação de Mudança: {resumo}" });
                var msg = new Mensagem { RemetenteId = UserId, DestinatarioId = servico.FornecedorId.Value, Conteudo = msgContent };
                _db.Mensagens.Add(msg);
                await _db.SaveChangesAsync();

                var payload = new { id = msg.Id, conteudo = msgContent, remetenteId = UserId, destinatarioId = servico.FornecedorId.Value, enviadaEm = msg.EnviadaEm.ToLocalTime().ToString("HH:mm"), lida = false, nomeRemetente = eu?.NomeCompleto ?? "" };
                await _hub.Clients.Group($"user-{servico.FornecedorId.Value}").SendAsync("ReceiveMessage", payload);
                await _hub.Clients.Group($"user-{UserId}").SendAsync("ReceiveMessage", payload);

                await _notif.CriarAsync(servico.FornecedorId.Value,
                    "Mudança solicitada.",
                    url: $"/Supplier/Chat?contatoId={UserId}", servicoId: servicoId);
            }
            await _log.RegistrarAsync(UserId, $"Solicitou mudança no serviço #{servicoId}.");
            TempData["Success"] = "Solicitação de mudança enviada!";
            return RedirectToAction("MeusServicos");
        }

        // Perfil público do contato (para modal de chat)
        [HttpGet]
        public async Task<IActionResult> GetContatoPublico(int id)
        {
            var u = await _db.Usuarios.FindAsync(id);
            if (u == null) return NotFound();
            return Json(new { u.NomeCompleto, u.Email, u.Telefone, u.SobreVoce,
                u.LinkedInUrl, u.WhatsAppNumero, isOnline = u.IsOnline, role = u.Role.ToString() });
        }

        // Detalhes do serviço ativo (modal de chat)
        [HttpGet]
        public async Task<IActionResult> GetServicoAtivoDetalhes(int fornecedorId)
        {
            var s = await _db.Servicos
                .FirstOrDefaultAsync(s => s.ClienteId == UserId && s.FornecedorId == fornecedorId
                                       && s.PagamentoInicialFeito
                                       && s.Status != ServicoStatus.Cancelado
                                       && s.Status != ServicoStatus.Concluido);
            if (s == null) return Json(new { ok = false, erro = "Nenhum serviço ativo encontrado." });
            string tipoNome = s.Tipo switch {
                TipoServico.Ecommerce => "E-commerce", TipoServico.Blog => "Blog",
                TipoServico.Designing => "Web Design", TipoServico.Marketing => "Marketing",
                TipoServico.SiteInstitucional => "Sites Institucionais", TipoServico.LandingPage => "Landing Pages",
                TipoServico.OnePage => "One-Page Sites", TipoServico.Portfolio => "Portfólios Digitais",
                TipoServico.BlogPortal => "Blogs e Portais", TipoServico.PlataformaEAD => "Plataformas EAD",
                TipoServico.LinkBio => "Links para Bio", TipoServico.SistemaWeb => "Sistemas Web",
                TipoServico.Agendamento => "Agendamento", TipoServico.MultiIdioma => "Multi-idiomas",
                TipoServico.Redesign => "Redesign", TipoServico.Manutencao => "Manutenção",
                TipoServico.SEO => "SEO & Performance", TipoServico.Migracao => "Migração",
                _ => s.Tipo.ToString()
            };
            string statusLabel = s.Status switch {
                ServicoStatus.Ativo => "Em andamento",
                ServicoStatus.AguardandoPagamentoFinal => "Aguardando pagamento final",
                _ => s.Status.ToString()
            };
            return Json(new { ok = true, s.Id, nome = s.NomeProjeto, tipo = tipoNome, status = statusLabel,
                valor    = s.Valor.HasValue ? s.Valor.Value.ToString("N2") : null,
                entrega  = s.DataEntrega.HasValue ? s.DataEntrega.Value.ToLocalTime().ToString("dd/MM/yyyy") : null,
                obs      = s.Observacoes });
        }

        // Concluir serviço via chat (JSON)
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ConcluirServicoChatJson(int id)
        {
            var servico = await _db.Servicos.FirstOrDefaultAsync(s => s.Id == id && s.ClienteId == UserId);
            if (servico == null) return Json(new { ok = false, erro = "Serviço não encontrado." });
            if (servico.Status != ServicoStatus.Ativo || !servico.PagamentoInicialFeito)
                return Json(new { ok = false, erro = "Este serviço não pode ser concluído agora." });
            servico.Status = ServicoStatus.AguardandoPagamentoFinal;
            servico.AtualizadoEm = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            if (servico.FornecedorId.HasValue)
                await _notif.CriarAsync(servico.FornecedorId.Value, "Entrega confirmada.", servicoId: id);
            var admins = await _db.Usuarios.Where(u => u.Role == UserRole.Admin).ToListAsync();
            foreach (var adm in admins)
                await _notif.CriarAsync(adm.Id, "Entrega confirmada.", url: "/Admin/Pedidos", servicoId: id);
            await _log.RegistrarAsync(UserId, $"Confirmou entrega do serviço #{id} via chat.");
            return Json(new { ok = true, msg = "Entrega confirmada! O admin processará o pagamento final." });
        }

        // Cancelar serviço via chat (JSON)
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelarServicoChatJson(int id)
        {
            var servico = await _db.Servicos.FirstOrDefaultAsync(s => s.Id == id && s.ClienteId == UserId);
            if (servico == null) return Json(new { ok = false, erro = "Serviço não encontrado." });
            if (servico.Status == ServicoStatus.Concluido || servico.Status == ServicoStatus.Cancelado)
                return Json(new { ok = false, erro = "Este projeto não pode ser cancelado." });
            string infoReembolso = "";
            if (servico.PagamentoInicialFeito && servico.PagamentoInicialEm.HasValue)
            {
                var valorPago = servico.Valor.GetValueOrDefault() * 0.3m;
                var (percMulta, percReembolso) = StripeService.CalcularMulta(servico.PagamentoInicialEm.Value);
                var valorReembolso = Math.Round(valorPago * percReembolso, 2);
                infoReembolso = valorReembolso > 0
                    ? $" Reembolso de R$ {valorReembolso:N2} será processado em até 5 dias úteis."
                    : " Cancelamento após 30 dias: nenhum reembolso aplicável.";
            }
            var notifs = await _db.Notificacoes.Where(n => n.ServicoId == id).ToListAsync();
            _db.Notificacoes.RemoveRange(notifs);
            var fId = servico.FornecedorId;
            if (fId.HasValue)
            {
                var msgs = await _db.Mensagens
                    .Where(m => (m.RemetenteId == UserId && m.DestinatarioId == fId.Value) ||
                                (m.RemetenteId == fId.Value && m.DestinatarioId == UserId))
                    .ToListAsync();
                _db.Mensagens.RemoveRange(msgs);
            }
            servico.Status = ServicoStatus.Cancelado;
            servico.AtualizadoEm = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            if (fId.HasValue)
            {
                await _hub.Clients.Group($"user-{UserId}").SendAsync("ConversationCleared", new { contatoId = fId.Value });
                await _hub.Clients.Group($"user-{fId.Value}").SendAsync("ConversationCleared", new { contatoId = UserId });
            }
            await _log.RegistrarAsync(UserId, $"Cancelou serviço #{id} via chat.");
            return Json(new { ok = true, msg = $"Projeto cancelado.{infoReembolso}" });
        }

        // Solicitar Mudança via Chat (modal)
        [Authorize(Roles = "Cliente"), HttpGet]
        public async Task<IActionResult> GetServicoAtivo(int fornecedorId)
        {
            var s = await _db.Servicos.FirstOrDefaultAsync(s =>
                s.ClienteId == UserId && s.FornecedorId == fornecedorId &&
                (s.Status == ServicoStatus.Ativo || s.Status == ServicoStatus.MudancaSolicitada));
            if (s == null) return Json(null);
            return Json(new { s.Id, s.NomeProjeto, s.Requisitos, s.Observacoes, tipo = s.Tipo.ToString() });
        }

        [Authorize(Roles = "Cliente"), HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SolicitarMudancaChat(int servicoId, int fornecedorId,
            string? nomeProjeto, string? observacoes, string novasInstrucoes)
        {
            var servico = await _db.Servicos.FirstOrDefaultAsync(s =>
                s.Id == servicoId && s.ClienteId == UserId &&
                (s.Status == ServicoStatus.Ativo || s.Status == ServicoStatus.MudancaSolicitada));
            if (servico == null) return Json(new { ok = false, erro = "Projeto não encontrado." });
            if (string.IsNullOrWhiteSpace(novasInstrucoes))
                return Json(new { ok = false, erro = "Descreva o que precisa mudar." });

            var campos = new List<object>();
            var nomeNovo = nomeProjeto?.Trim();
            if (!string.IsNullOrEmpty(nomeNovo) && nomeNovo != servico.NomeProjeto)
                campos.Add(new { campo = "Nome do Projeto", de = servico.NomeProjeto, para = nomeNovo });
            var obsNova = observacoes?.Trim();
            if (obsNova != (servico.Observacoes ?? ""))
                campos.Add(new { campo = "Observações", de = servico.Observacoes, para = obsNova });
            campos.Add(new { campo = "Novas Instruções", de = (string?)null, para = novasInstrucoes.Trim() });

            var descJson = JsonSerializer.Serialize(campos);
            var mudanca  = new SolicitacaoMudanca { ServicoId = servicoId, SolicitanteId = UserId, Descricao = descJson };
            _db.SolicitacoesMudanca.Add(mudanca);
            servico.Status       = ServicoStatus.MudancaSolicitada;
            servico.AtualizadoEm = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            var eu       = await _db.Usuarios.FindAsync(UserId);
            var preview  = novasInstrucoes.Length > 60 ? novasInstrucoes[..60] + "..." : novasInstrucoes;
            var msgJson  = JsonSerializer.Serialize(new { _t = "M", id = mudanca.Id, r = $"Solicitação de Mudança: {preview}" });
            var msg      = new Mensagem { RemetenteId = UserId, DestinatarioId = fornecedorId, Conteudo = msgJson };
            _db.Mensagens.Add(msg);
            await _db.SaveChangesAsync();

            var payload = new { id = msg.Id, conteudo = msgJson, remetenteId = UserId, destinatarioId = fornecedorId, enviadaEm = msg.EnviadaEm.ToLocalTime().ToString("HH:mm"), lida = false, nomeRemetente = eu?.NomeCompleto ?? "" };
            await _hub.Clients.Group($"user-{fornecedorId}").SendAsync("ReceiveMessage", payload);
            await _hub.Clients.Group($"user-{UserId}").SendAsync("ReceiveMessage", payload);

            await _notif.CriarAsync(fornecedorId,
                "Mudança solicitada.",
                url: $"/Supplier/Chat?contatoId={UserId}", servicoId: servicoId);
            await _log.RegistrarAsync(UserId, $"Solicitou mudança no serviço #{servicoId} via chat.");
            return Json(new { ok = true });
        }

        // API: info de mudança (cliente vê mudança que criou)
        [Authorize(Roles = "Cliente"), HttpGet]
        public async Task<IActionResult> GetMudancaInfo(int id)
        {
            var m = await _db.SolicitacoesMudanca
                .Include(x => x.Servico)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (m == null || m.Servico?.ClienteId != UserId) return NotFound();
            return Json(new { m.Id, m.Descricao, m.Aceita, m.Recusada });
        }

        // API: notificações (JSON)
        [HttpGet]
        public async Task<IActionResult> Notificacoes()
        {
            var list = await _db.Notificacoes
                .Where(n => n.UsuarioId == UserId && !n.Lida)
                .OrderByDescending(n => n.CriadaEm)
                .Take(10)
                .Select(n => new { n.Id, n.Titulo, n.Corpo, n.Url, n.CriadaEm })
                .ToListAsync();
            return Json(list);
        }

        [HttpPost]
        public async Task<IActionResult> MarcarNotificacaoLida(int id)
        {
            var n = await _db.Notificacoes.FirstOrDefaultAsync(n => n.Id == id && n.UsuarioId == UserId);
            if (n != null) { _db.Notificacoes.Remove(n); await _db.SaveChangesAsync(); }
            return Ok();
        }

        // Upload de Arquivo
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadArquivo(int servicoId, IFormFile arquivo)
        {
            var servico = await _db.Servicos.FirstOrDefaultAsync(s =>
                s.Id == servicoId && s.ClienteId == UserId);
            if (servico == null) return NotFound();

            if (arquivo == null || arquivo.Length == 0)
            {
                TempData["Erro"] = "Nenhum arquivo selecionado.";
                return RedirectToAction("MeusServicos");
            }
            if (arquivo.Length > 10 * 1024 * 1024)
            {
                TempData["Erro"] = "O arquivo excede o limite de 10 MB.";
                return RedirectToAction("MeusServicos");
            }

            var ext = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
            var permitidos = new[] { ".pdf",".doc",".docx",".xls",".xlsx",".png",".jpg",".jpeg",".gif",".zip",".txt" };
            if (!permitidos.Contains(ext))
            {
                TempData["Erro"] = "Tipo de arquivo não permitido.";
                return RedirectToAction("MeusServicos");
            }

            var pasta = Path.Combine(_env.WebRootPath, "uploads", servicoId.ToString());
            Directory.CreateDirectory(pasta);

            var nomeArquivo = $"{Guid.NewGuid():N}{ext}";
            var caminhoFisico = Path.Combine(pasta, nomeArquivo);
            await using (var fs = new FileStream(caminhoFisico, FileMode.Create))
                await arquivo.CopyToAsync(fs);

            _db.Arquivos.Add(new Arquivo
            {
                NomeOriginal = Path.GetFileName(arquivo.FileName),
                Caminho      = $"/uploads/{servicoId}/{nomeArquivo}",
                MimeType     = arquivo.ContentType,
                Tamanho      = arquivo.Length,
                ServicoId    = servicoId,
                UsuarioId    = UserId
            });
            await _db.SaveChangesAsync();

            TempData["Success"] = "Arquivo enviado com sucesso!";
            return RedirectToAction("MeusServicos");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoverArquivo(int id)
        {
            var arq = await _db.Arquivos
                .Include(a => a.Servico)
                .FirstOrDefaultAsync(a => a.Id == id && a.UsuarioId == UserId);
            if (arq == null) return NotFound();

            var caminhoFisico = Path.Combine(_env.WebRootPath, arq.Caminho.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(caminhoFisico))
                System.IO.File.Delete(caminhoFisico);

            _db.Arquivos.Remove(arq);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Arquivo removido.";
            return RedirectToAction("MeusServicos");
        }

        // Avaliar Fornecedor + Plataforma (2 passos juntos)
        [Authorize(Roles = "Cliente"), HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AvaliarCompleto(AvaliarCompletoVM vm)
        {
            var uid = UserId;
            var servico = await _db.Servicos
                .Include(s => s.Avaliacao)
                .Include(s => s.AvaliacoesSistema)
                .FirstOrDefaultAsync(s => s.Id == vm.ServicoId && s.ClienteId == uid && s.Status == ServicoStatus.Concluido);
            if (servico == null) return NotFound();

            if (!servico.FornecedorId.HasValue)
            {
                TempData["Erro"] = "Projeto sem fornecedor vinculado.";
                return RedirectToAction("MeusServicos");
            }

            // Avaliação do fornecedor (se ainda não feita e estrelas válidas)
            if (servico.Avaliacao == null && vm.EstrelasFornecedor >= 1 && vm.EstrelasFornecedor <= 5)
            {
                _db.Avaliacoes.Add(new Avaliacao
                {
                    ServicoId    = vm.ServicoId,
                    ClienteId    = uid,
                    FornecedorId = servico.FornecedorId.Value,
                    Estrelas     = vm.EstrelasFornecedor,
                    Comentario   = vm.ComentarioFornecedor?.Trim()
                });
                await _notif.CriarAsync(servico.FornecedorId.Value,
                    "Avaliação recebida!",
                    url: "/Supplier/Portfolio", servicoId: vm.ServicoId);
                await _log.RegistrarAsync(uid, $"Avaliou fornecedor no serviço #{vm.ServicoId} com {vm.EstrelasFornecedor} estrela(s).");
            }

            // Avaliação do sistema (se ainda não feita)
            bool jaAvaliouSistema = servico.AvaliacoesSistema.Any(a => a.UsuarioId == uid);
            if (!jaAvaliouSistema && vm.EstrelasSistema >= 1 && vm.EstrelasSistema <= 5)
            {
                _db.AvaliacoesSistema.Add(new AvaliacaoSistema
                {
                    ServicoId  = vm.ServicoId,
                    UsuarioId  = uid,
                    Estrelas   = vm.EstrelasSistema,
                    Comentario = vm.ComentarioSistema?.Trim()
                });
                await _log.RegistrarAsync(uid, $"Avaliou a plataforma com {vm.EstrelasSistema} estrela(s) no serviço #{vm.ServicoId}.");
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = "Avaliação enviada! Obrigado pelo feedback.";
            return RedirectToAction("MeusServicos");
        }

        // Avaliar só a Plataforma (quando fornecedor já foi avaliado)
        [Authorize(Roles = "Cliente"), HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AvaliarSistema(AvaliarSistemaVM vm)
        {
            var uid = UserId;
            var servico = await _db.Servicos
                .Include(s => s.AvaliacoesSistema)
                .FirstOrDefaultAsync(s => s.Id == vm.ServicoId && s.ClienteId == uid && s.Status == ServicoStatus.Concluido);
            if (servico == null) return NotFound();

            if (servico.AvaliacoesSistema.Any(a => a.UsuarioId == uid))
            {
                TempData["Erro"] = "Você já avaliou a plataforma para este projeto.";
                return RedirectToAction("MeusServicos");
            }

            _db.AvaliacoesSistema.Add(new AvaliacaoSistema
            {
                ServicoId  = vm.ServicoId,
                UsuarioId  = uid,
                Estrelas   = vm.Estrelas,
                Comentario = vm.Comentario?.Trim()
            });
            await _db.SaveChangesAsync();
            await _log.RegistrarAsync(uid, $"Avaliou a plataforma com {vm.Estrelas} estrela(s) no serviço #{vm.ServicoId}.");

            TempData["Success"] = "Obrigado por avaliar a plataforma!";
            return RedirectToAction("MeusServicos");
        }
    }
}
