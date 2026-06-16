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
using AvaliarSistemaVM = MindSite.ViewModels.AvaliarSistemaViewModel;
using ItemPortfolioVM = MindSite.ViewModels.ItemPortfolioViewModel;

namespace MindSite.Controllers
{
    [Authorize(Roles = "Fornecedor")]
    public class SupplierController : Controller
    {
        private readonly AppDbContext _db;
        private readonly NotificacaoService _notif;
        private readonly LogService _log;
        private readonly IHubContext<ChatHub> _hub;
        private readonly IWebHostEnvironment _env;

        public SupplierController(AppDbContext db, NotificacaoService notif, LogService log, IHubContext<ChatHub> hub, IWebHostEnvironment env)
        {
            _db = db;
            _notif = notif;
            _log = log;
            _hub = hub;
            _env = env;
        }

        private int UserId => int.Parse(User.FindFirstValue("UserId")!);

        // Home
        public async Task<IActionResult> Index()
        {
            var u = await _db.Usuarios.Include(x => x.Empresa).FirstOrDefaultAsync(x => x.Id == UserId);
            var uid = UserId;
            ViewBag.Pendentes = await _db.Servicos.CountAsync(s =>
                s.Status == ServicoStatus.AguardandoPropostas &&
                s.Propostas.Count < 3 &&
                !s.Propostas.Any(p => p.FornecedorId == uid));
            ViewBag.EmAndamento = await _db.Servicos.CountAsync(s => s.FornecedorId == uid && s.Status == ServicoStatus.Ativo);
            return View(u);
        }

        // Serviços disponíveis
        public async Task<IActionResult> Servicos(string? filtro, int page = 1)
        {
            const int porPagina = 12;
            var uid = UserId;
            var query = _db.Servicos
                .Include(s => s.Cliente)
                .Include(s => s.Propostas)
                .Where(s => s.Cliente!.Status != UserStatus.Bloqueado)
                .AsQueryable();

            if (filtro == "meus")
            {
                query = query
                    .Where(s => s.FornecedorId == uid)
                    .Include(s => s.AvaliacoesSistema);
            }
            else
                query = query.Where(s =>
                    s.Status == ServicoStatus.AguardandoPropostas &&
                    s.Propostas.Count < 3 &&
                    !s.Propostas.Any(p => p.FornecedorId == uid));

            var total = await query.CountAsync();
            var servicos = await query
                .OrderByDescending(s => s.CriadoEm)
                .Skip((page - 1) * porPagina)
                .Take(porPagina)
                .ToListAsync();

            var fornecedor = await _db.Usuarios.FindAsync(uid);
            ViewBag.Filtro = filtro ?? "disponiveis";
            ViewBag.UserId = uid;
            ViewBag.NomeCompleto = fornecedor?.NomeCompleto;
            ViewBag.CPF = fornecedor?.CPF;
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)porPagina);
            return View(servicos);
        }

        // Detalhes de um serviço
        public async Task<IActionResult> DetalheServico(int id)
        {
            var s = await _db.Servicos
                .Include(x => x.Cliente)
                .Include(x => x.Mudancas).ThenInclude(m => m.Solicitante)
                .Include(x => x.Arquivos)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (s == null) return NotFound();
            if (s.Cliente?.Status == UserStatus.Bloqueado)
            {
                TempData["Erro"] = "Este cliente está bloqueado e você não pode acessar este serviço.";
                return RedirectToAction("Servicos");
            }
            ViewBag.FornecedorId = UserId;
            return View(s);
        }

        // Submeter Proposta
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmeterProposta(SubmeterPropostaViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                TempData["Erro"] = "Dados inválidos.";
                return RedirectToAction("Servicos");
            }

            var s = await _db.Servicos
                .Include(x => x.Propostas)
                .FirstOrDefaultAsync(x => x.Id == vm.ServicoId);
            if (s == null || s.Status != ServicoStatus.AguardandoPropostas)
            {
                TempData["Erro"] = "Serviço não disponível para propostas.";
                return RedirectToAction("Servicos");
            }

            if (s.Propostas.Count >= 3)
            {
                TempData["Erro"] = "Este serviço já atingiu o limite de 3 propostas.";
                return RedirectToAction("Servicos");
            }

            if (s.Propostas.Any(p => p.FornecedorId == UserId))
            {
                TempData["Erro"] = "Você já enviou uma proposta para este serviço.";
                return RedirectToAction("Servicos");
            }

            if (s.PrazoCliente == PrazoTipo.NaoSei)
            {
                if (!vm.DataEntrega.HasValue || vm.DataEntrega.Value.Date <= DateTime.Today)
                {
                    TempData["Erro"] = "O cliente deixou o prazo em aberto. Informe uma data de entrega posterior ao dia atual.";
                    return RedirectToAction("Servicos");
                }
            }

            DateTime dataEntregaFinal = ResolverDataEntrega(s, vm.DataEntrega);

            _db.Propostas.Add(new Proposta
            {
                ServicoId = vm.ServicoId,
                FornecedorId = UserId,
                Valor = s.OrcamentoCliente ?? 0,
                DataEntrega = dataEntregaFinal,
                Mensagem = vm.Mensagem?.Trim()
            });

            // Notifica admins a cada nova proposta
            var admins = await _db.Usuarios.Where(u => u.Role == UserRole.Admin).ToListAsync();
            foreach (var adm in admins)
                await _notif.CriarAsync(adm.Id, "Proposta(s) recebidas.", url: "/Admin/Pedidos", servicoId: vm.ServicoId);

            if (s.Propostas.Count + 1 >= 3)
            {
                s.Status = ServicoStatus.AguardandoEscolha;
                await _notif.CriarAsync(s.ClienteId,
                    "3 propostas recebidas!",
                    url: "/Client/MeusServicos", servicoId: vm.ServicoId);
            }

            s.AtualizadoEm = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await _log.RegistrarAsync(UserId, $"Submeteu proposta para serviço #{vm.ServicoId}.");

            TempData["Success"] = "Proposta enviada com sucesso!";
            return RedirectToAction("Servicos");
        }

        // Aceitar Serviço direto (Pendente sem fornecedor)
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AceitarServico(int id, decimal valor, DateTime? dataEntrega, string? observacoes)
        {
            var s = await _db.Servicos.Include(x => x.Cliente).FirstOrDefaultAsync(x => x.Id == id);
            if (s == null || s.FornecedorId != null || s.Status != ServicoStatus.Pendente)
            {
                TempData["Erro"] = "Serviço não disponível.";
                return RedirectToAction("Servicos");
            }

            if (s.PrazoCliente == PrazoTipo.NaoSei)
            {
                if (!dataEntrega.HasValue || dataEntrega.Value.Date <= DateTime.Today)
                {
                    TempData["Erro"] = "O cliente deixou o prazo em aberto. Informe uma data de entrega posterior ao dia atual.";
                    return RedirectToAction("DetalheServico", new { id });
                }
            }
            else if (dataEntrega.HasValue)
            {
                TempData["Erro"] = "O prazo já foi definido pelo cliente. Você não pode alterar a data.";
                return RedirectToAction("DetalheServico", new { id });
            }

            s.FornecedorId = UserId;
            s.Valor = valor;
            s.DataEntrega = s.PrazoCliente == PrazoTipo.NaoSei ? dataEntrega : ResolverDataEntrega(s, null);
            s.Observacoes = observacoes?.Trim();
            s.Status = ServicoStatus.Ativo;
            s.AtualizadoEm = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await _notif.CriarAsync(s.ClienteId,
                "Pedido aceito!",
                url: "/Client/MeusServicos", servicoId: id);
            await _log.RegistrarAsync(UserId, $"Aceitou serviço #{id}.");

            TempData["Success"] = "Serviço aceito com sucesso!";
            return RedirectToAction("DetalheServico", new { id });
        }

        private static DateTime ResolverDataEntrega(Servico s, DateTime? dataFornecedor) =>
            s.PrazoCliente switch
            {
                PrazoTipo.NaoSei => dataFornecedor!.Value,
                PrazoTipo.DataEspecifica => s.DataPrazoCliente!.Value,
                PrazoTipo.Meses1a3 => DateTime.Today.AddMonths(3),
                PrazoTipo.Ate6Meses => DateTime.Today.AddMonths(6),
                PrazoTipo.Ate1Ano => DateTime.Today.AddMonths(12),
                PrazoTipo.MaisDeUmAno => DateTime.Today.AddMonths(18),
                _ => DateTime.Today.AddMonths(3)
            };

        // Enviar Serviço (reenvio)
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EnviarServico(int id, decimal novoValor, DateTime novaData, string? observacoes)
        {
            var s = await _db.Servicos.FindAsync(id);
            if (s == null || s.FornecedorId != UserId) return NotFound();

            s.Valor = novoValor;
            s.DataEntrega = novaData;
            s.Observacoes = observacoes;
            s.Status = ServicoStatus.Ativo;
            s.AtualizadoEm = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await _notif.CriarAsync(s.ClienteId,
                "Projeto atualizado.",
                url: "/Client/MeusServicos", servicoId: id);
            await _log.RegistrarAsync(UserId, $"Atualizou serviço #{id}.");

            TempData["Success"] = "Serviço atualizado!";
            return RedirectToAction("DetalheServico", new { id });
        }

        // API: detalhes de mudança (para modal do chat)
        [HttpGet]
        public async Task<IActionResult> GetMudancaInfo(int id)
        {
            var m = await _db.SolicitacoesMudanca
                .Include(x => x.Servico).ThenInclude(s => s!.Cliente)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (m == null || m.Servico?.FornecedorId != UserId) return NotFound();
            return Json(new
            {
                m.Id,
                m.Descricao,
                m.Aceita,
                m.Recusada,
                m.ServicoId,
                clienteId = m.Servico!.ClienteId,
                clienteNome = m.Servico.Cliente?.NomeCompleto
            });
        }

        // Responder Mudança
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ResponderMudanca(int mudancaId, bool aceitar, string? resposta)
        {
            var m = await _db.SolicitacoesMudanca
                .Include(x => x.Servico)
                .FirstOrDefaultAsync(x => x.Id == mudancaId);
            if (m == null) return NotFound();

            m.Aceita = aceitar;
            m.Recusada = !aceitar;
            m.RespostaFornecedor = resposta?.Trim();

            var eu = await _db.Usuarios.FindAsync(UserId);

            if (aceitar)
            {
                // Notifica admins para processar (adicionar valor, se houver)
                var admins = await _db.Usuarios.Where(u => u.Role == UserRole.Admin).ToListAsync();
                foreach (var adm in admins)
                    await _notif.CriarAsync(adm.Id,
                        "Mudança aceita.",
                        url: $"/Admin/Pedidos?mudancaId={mudancaId}", servicoId: m.ServicoId);
            }
            else
            {
                // Manda mensagem de rejeição automática para o cliente no chat
                if (m.Servico != null)
                {
                    m.Servico.Status = ServicoStatus.Ativo;
                    m.Servico.AtualizadoEm = DateTime.UtcNow;

                    var justif = resposta?.Trim() ?? "Sem justificativa informada.";
                    var msgJson = JsonSerializer.Serialize(new { _t = "R", id = mudancaId, r = "Solicitação Rejeitada", j = justif });
                    var msgRej = new Mensagem { RemetenteId = UserId, DestinatarioId = m.Servico.ClienteId, Conteudo = msgJson };
                    _db.Mensagens.Add(msgRej);
                    await _db.SaveChangesAsync();

                    var payload = new { id = msgRej.Id, conteudo = msgJson, remetenteId = UserId, destinatarioId = m.Servico.ClienteId, enviadaEm = msgRej.EnviadaEm.ToLocalTime().ToString("HH:mm"), lida = false, nomeRemetente = eu?.NomeCompleto ?? "" };
                    await _hub.Clients.Group($"user-{m.Servico.ClienteId}").SendAsync("ReceiveMessage", payload);
                    await _hub.Clients.Group($"user-{UserId}").SendAsync("ReceiveMessage", payload);

                    await _notif.CriarAsync(m.Servico.ClienteId,
                        "Mudança recusada.",
                        url: $"/Client/Chat?contatoId={UserId}", servicoId: m.ServicoId);
                }
            }

            await _db.SaveChangesAsync();
            await _log.RegistrarAsync(UserId, $"Respondeu mudança #{mudancaId}: {(aceitar ? "Aceita" : "Recusada")}.");

            return Json(new { ok = true, aceita = aceitar });
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

        // Detalhes do serviço ativo (modal de chat)
        [HttpGet]
        public async Task<IActionResult> GetServicoAtivoDetalhes(int clienteId)
        {
            var s = await _db.Servicos
                .FirstOrDefaultAsync(s => s.FornecedorId == UserId && s.ClienteId == clienteId
                                       && s.PagamentoInicialFeito
                                       && s.Status != ServicoStatus.Cancelado
                                       && s.Status != ServicoStatus.Concluido);
            if (s == null) return Json(new { ok = false, erro = "Nenhum serviço ativo encontrado." });
            string tipoNome = s.Tipo switch
            {
                TipoServico.Ecommerce => "E-commerce",
                TipoServico.Blog => "Blog",
                TipoServico.Designing => "Web Design",
                TipoServico.Marketing => "Marketing",
                TipoServico.SiteInstitucional => "Sites Institucionais",
                TipoServico.LandingPage => "Landing Pages",
                TipoServico.OnePage => "One-Page Sites",
                TipoServico.Portfolio => "Portfólios Digitais",
                TipoServico.BlogPortal => "Blogs e Portais",
                TipoServico.PlataformaEAD => "Plataformas EAD",
                TipoServico.LinkBio => "Links para Bio",
                TipoServico.SistemaWeb => "Sistemas Web",
                TipoServico.Agendamento => "Agendamento",
                TipoServico.MultiIdioma => "Multi-idiomas",
                TipoServico.Redesign => "Redesign",
                TipoServico.Manutencao => "Manutenção",
                TipoServico.SEO => "SEO & Performance",
                TipoServico.Migracao => "Migração",
                _ => s.Tipo.ToString()
            };
            string statusLabel = s.Status switch
            {
                ServicoStatus.Ativo => "Em andamento",
                ServicoStatus.AguardandoPagamentoFinal => "Aguardando pagamento final",
                _ => s.Status.ToString()
            };
            return Json(new
            {
                ok = true,
                s.Id,
                nome = s.NomeProjeto,
                tipo = tipoNome,
                status = statusLabel,
                valor = s.Valor.HasValue ? s.Valor.Value.ToString("N2") : null,
                entrega = s.DataEntrega.HasValue ? s.DataEntrega.Value.ToLocalTime().ToString("dd/MM/yyyy") : null,
                obs = s.Observacoes
            });
        }

      // Chat
        public async Task<IActionResult> Chat(int? contatoId, bool grupoSuporte = false)
        {
            var uid = UserId;
            var eu = await _db.Usuarios.FindAsync(uid);

            // Clientes cujo pagamento inicial foi confirmado (sem admins)
            var clienteIds = await _db.Servicos
                .Where(s => s.FornecedorId == uid && s.PagamentoInicialFeito)
                .Select(s => s.ClienteId)
                .Distinct()
                .ToListAsync();
            var clientesAtivos = await _db.Usuarios
                .Where(u => clienteIds.Contains(u.Id))
                .OrderBy(u => u.NomeCompleto)
                .ToListAsync();

            var contatosPinados = clientesAtivos;
            var pinadosIds = contatosPinados.Select(c => c.Id).ToHashSet();

            // Outras conversas (não pinadas, sem admins)
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
                    Contato = contato,
                    UltimaMensagem = ult?.Conteudo ?? "",
                    Quando = ult?.EnviadaEm,
                    NaoLidas = naoLidas
                });
            }

            // Atualiza contagem de não-lidas para pinados
            foreach (var p in contatosPinados)
            {
                var ult = await _db.Mensagens
                    .Where(m => (m.RemetenteId == uid && m.DestinatarioId == p.Id) ||
                                (m.RemetenteId == p.Id && m.DestinatarioId == uid))
                    .OrderByDescending(m => m.EnviadaEm).FirstOrDefaultAsync();
                var naoLidas = await _db.Mensagens
                    .CountAsync(m => m.RemetenteId == p.Id && m.DestinatarioId == uid && !m.Lida);
                conversas.Insert(0, new ConversaItem
                {
                    Contato = p,
                    UltimaMensagem = ult?.Conteudo ?? "",
                    Quando = ult?.EnviadaEm,
                    NaoLidas = naoLidas,
                    Pinado = true
                });
            }

            // Grupo Suporte
            List<MensagemGrupo> grupoMensagens = new();
            var ultGrupo = await _db.MensagensGrupo
                .Where(m => m.ThreadUserId == uid && !m.EhSistema)
                .OrderByDescending(m => m.EnviadaEm)
                .FirstOrDefaultAsync();
            var grupoUltimaMensagem = ultGrupo?.Conteudo ?? "";
            DateTime? grupoUltimaData = ultGrupo?.EnviadaEm;

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
            if (contatoId.HasValue && !grupoSuporte)
            {
                contatoSel = await _db.Usuarios.FindAsync(contatoId.Value);

                // Bloqueia acesso ao chat com cliente sem pagamento inicial confirmado
                if (contatoSel?.Role == UserRole.Cliente)
                {
                    var temAcesso = await _db.Servicos.AnyAsync(s =>
                        s.FornecedorId == uid &&
                        s.ClienteId == contatoId.Value &&
                        s.PagamentoInicialFeito);
                    if (!temAcesso)
                        return RedirectToAction("Chat");

                    var statusAtivos = new List<ServicoStatus> { ServicoStatus.Ativo, ServicoStatus.MudancaSolicitada };
                    chatBloqueado = !await _db.Servicos.AnyAsync(s =>
                        s.FornecedorId == uid &&
                        s.ClienteId == contatoId.Value &&
                        statusAtivos.Contains(s.Status));
                }

                mensagens = await _db.Mensagens
                    .Include(m => m.Remetente)
                    .Where(m => (m.RemetenteId == uid && m.DestinatarioId == contatoId) ||
                                (m.RemetenteId == contatoId && m.DestinatarioId == uid))
                    .OrderBy(m => m.EnviadaEm).ToListAsync();

                foreach (var mm in mensagens.Where(m => m.DestinatarioId == uid && !m.Lida))
                    mm.Lida = true;
                await _db.SaveChangesAsync();
            }

            return View(new ChatViewModel
            {
                UsuarioAtual = eu ?? new Usuario(),
                Conversas = conversas.OrderByDescending(c => c.Pinado).ThenByDescending(c => c.Quando).ToList(),
                ContatoSelecionadoId = contatoId,
                ContatoSelecionado = contatoSel,
                Mensagens = mensagens,
                ContatosPinados = contatosPinados,
                IsGrupoSuporte = grupoSuporte,
                GrupoMensagens = grupoMensagens,
                GrupoUltimaMensagem = grupoUltimaMensagem,
                GrupoUltimaData = grupoUltimaData,
                ChatBloqueado = chatBloqueado
            });
        }


        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EnviarMensagem(EnviarMensagemViewModel vm)
        {
            if (!ModelState.IsValid) return RedirectToAction("Chat", new { contatoId = vm.DestinatarioId });
            _db.Mensagens.Add(new Mensagem
            {
                RemetenteId = UserId,
                DestinatarioId = vm.DestinatarioId,
                Conteudo = vm.Conteudo.Trim()
            });
            await _db.SaveChangesAsync();

            var dest = await _db.Usuarios.FindAsync(vm.DestinatarioId);
            var chatUrl = dest?.Role switch
            {
                UserRole.Admin => $"/Admin/Chat?contatoId={UserId}",
                UserRole.Fornecedor => $"/Supplier/Chat?contatoId={UserId}",
                _ => $"/Client/Chat?contatoId={UserId}"
            };
            await _notif.CriarAsync(vm.DestinatarioId, "Mensagem recebida.", url: chatUrl);
            return RedirectToAction("Chat", new { contatoId = vm.DestinatarioId });
        }

        // Notificações (API)
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
                s.Id == servicoId && s.FornecedorId == UserId);
            if (servico == null) return NotFound();

            if (arquivo == null || arquivo.Length == 0)
            {
                TempData["Erro"] = "Nenhum arquivo selecionado.";
                return RedirectToAction("DetalheServico", new { id = servicoId });
            }
            if (arquivo.Length > 10 * 1024 * 1024)
            {
                TempData["Erro"] = "O arquivo excede o limite de 10 MB.";
                return RedirectToAction("DetalheServico", new { id = servicoId });
            }

            var ext = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
            var permitidos = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg", ".gif", ".zip", ".txt" };
            if (!permitidos.Contains(ext))
            {
                TempData["Erro"] = "Tipo de arquivo não permitido.";
                return RedirectToAction("DetalheServico", new { id = servicoId });
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
                Caminho = $"/uploads/{servicoId}/{nomeArquivo}",
                MimeType = arquivo.ContentType,
                Tamanho = arquivo.Length,
                ServicoId = servicoId,
                UsuarioId = UserId
            });
            await _db.SaveChangesAsync();

            TempData["Success"] = "Arquivo enviado com sucesso!";
            return RedirectToAction("DetalheServico", new { id = servicoId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoverArquivo(int id, int servicoId)
        {
            var arq = await _db.Arquivos
                .FirstOrDefaultAsync(a => a.Id == id && a.UsuarioId == UserId);
            if (arq == null) return NotFound();

            var caminhoFisico = Path.Combine(_env.WebRootPath, arq.Caminho.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(caminhoFisico))
                System.IO.File.Delete(caminhoFisico);

            _db.Arquivos.Remove(arq);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Arquivo removido.";
            return RedirectToAction("DetalheServico", new { id = servicoId });
        }

        // Portfólio
        public async Task<IActionResult> Portfolio()
        {
            var items = await _db.Portfolios
                .Where(p => p.FornecedorId == UserId)
                .OrderByDescending(p => p.CriadoEm)
                .ToListAsync();
            return View(items);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarPortfolio(ItemPortfolioVM vm, IFormFile? imagem)
        {
            if (!ModelState.IsValid)
            {
                TempData["Erro"] = "Preencha o título do item.";
                return RedirectToAction("Portfolio");
            }

            string? imagemCaminho = null;
            if (imagem != null && imagem.Length > 0)
            {
                var ext = Path.GetExtension(imagem.FileName).ToLowerInvariant();
                var imgPermitidos = new[] { ".png", ".jpg", ".jpeg", ".gif", ".webp" };
                if (!imgPermitidos.Contains(ext))
                {
                    TempData["Erro"] = "Apenas imagens são permitidas (png, jpg, gif, webp).";
                    return RedirectToAction("Portfolio");
                }
                if (imagem.Length > 5 * 1024 * 1024)
                {
                    TempData["Erro"] = "A imagem deve ter no máximo 5 MB.";
                    return RedirectToAction("Portfolio");
                }

                var pasta = Path.Combine(_env.WebRootPath, "uploads", "portfolio", UserId.ToString());
                Directory.CreateDirectory(pasta);
                var nomeArquivo = $"{Guid.NewGuid():N}{ext}";
                var caminhoFisico = Path.Combine(pasta, nomeArquivo);
                await using (var fs = new FileStream(caminhoFisico, FileMode.Create))
                    await imagem.CopyToAsync(fs);
                imagemCaminho = $"/uploads/portfolio/{UserId}/{nomeArquivo}";
            }

            _db.Portfolios.Add(new ItemPortfolio
            {
                Titulo = vm.Titulo.Trim(),
                Descricao = vm.Descricao?.Trim(),
                LinkUrl = vm.LinkUrl?.Trim(),
                ImagemCaminho = imagemCaminho,
                FornecedorId = UserId
            });
            await _db.SaveChangesAsync();

            TempData["Success"] = "Item adicionado ao portfólio!";
            return RedirectToAction("Portfolio");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoverPortfolio(int id)
        {
            var item = await _db.Portfolios.FirstOrDefaultAsync(p => p.Id == id && p.FornecedorId == UserId);
            if (item == null) return NotFound();

            if (!string.IsNullOrEmpty(item.ImagemCaminho))
            {
                var caminhoFisico = Path.Combine(_env.WebRootPath, item.ImagemCaminho.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(caminhoFisico))
                    System.IO.File.Delete(caminhoFisico);
            }

            _db.Portfolios.Remove(item);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Item removido do portfólio.";
            return RedirectToAction("Portfolio");
        }

        // Avaliar a Plataforma (fornecedor)
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AvaliarSistema(AvaliarSistemaVM vm)
        {
            var uid = UserId;
            var servico = await _db.Servicos
                .Include(s => s.AvaliacoesSistema)
                .FirstOrDefaultAsync(s => s.Id == vm.ServicoId && s.FornecedorId == uid && s.Status == ServicoStatus.Concluido);
            if (servico == null) return NotFound();

            if (servico.AvaliacoesSistema.Any(a => a.UsuarioId == uid))
            {
                TempData["Erro"] = "Você já avaliou a plataforma para este projeto.";
                return RedirectToAction("Servicos", new { filtro = "meus" });
            }

            _db.AvaliacoesSistema.Add(new AvaliacaoSistema
            {
                ServicoId = vm.ServicoId,
                UsuarioId = uid,
                Estrelas = vm.Estrelas,
                Comentario = vm.Comentario?.Trim()
            });
            await _db.SaveChangesAsync();
            await _log.RegistrarAsync(uid, $"Avaliou a plataforma com {vm.Estrelas} estrela(s) no serviço #{vm.ServicoId}.");

            TempData["Success"] = "Obrigado por avaliar a plataforma!";
            return RedirectToAction("Servicos", new { filtro = "meus" });
        }
        [HttpGet]
        public async Task<IActionResult> ObterDetalhesRepasse(int id)
        {
            int fornecedorId = int.Parse(User.FindFirstValue("UserId")!);

            var servico = await _db.Servicos
                .FirstOrDefaultAsync(s => s.Id == id && s.FornecedorId == fornecedorId && s.Status == ServicoStatus.Concluido);

            if (servico == null) return NotFound();

            return Json(new
            {
                id = servico.Id,
                valorLiquido = servico.ValorRepasseFornecedor ?? 0,
                valorLiquidoFormatado = (servico.ValorRepasseFornecedor ?? 0).ToString("C", new System.Globalization.CultureInfo("pt-BR"))
            });
        }
    }
}
