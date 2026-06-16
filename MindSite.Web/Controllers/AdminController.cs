using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MindSite.Data;
using MindSite.Hubs;
using MindSite.Entities;
using MindSite.Services;
using MindSite.ViewModels;
using System.Security.Claims;
using System.Text.Json;
using MindSite.Enums;

namespace MindSite.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly AppDbContext        _db;
        private readonly NotificacaoService  _notif;
        private readonly LogService          _log;
        private readonly IHubContext<ChatHub> _hub;
        private readonly IWebHostEnvironment _env;

        public AdminController(AppDbContext db, NotificacaoService notif, LogService log, IHubContext<ChatHub> hub, IWebHostEnvironment env)
        {
            _db    = db;
            _notif = notif;
            _log   = log;
            _hub   = hub;
            _env   = env;
        }

        private int UserId => int.Parse(User.FindFirstValue("UserId")!);

        // Dashboard
        public async Task<IActionResult> Index()
        {
            var vm = new AdminDashboardViewModel
            {
                TotalClientes    = await _db.Usuarios.CountAsync(u => u.Role == UserRole.Cliente),
                TotalFornecedores = await _db.Usuarios.CountAsync(u => u.Role == UserRole.Fornecedor && u.Status == UserStatus.Ativo),
                ServicosPendentes = await _db.Servicos.CountAsync(s => s.Status == ServicoStatus.Pendente),
                ServicosAtivos    = await _db.Servicos.CountAsync(s => s.Status == ServicoStatus.Ativo),
                ServicosConcluidos = await _db.Servicos.CountAsync(s => s.Status == ServicoStatus.Concluido),
                FornecedoresPendentes = await _db.Usuarios.CountAsync(u => u.Role == UserRole.Fornecedor && u.Status == UserStatus.Pendente),
                AcoesRecentes = await _db.Logs
                    .Include(l => l.Usuario)
                    .OrderByDescending(l => l.RealizadaEm)
                    .Take(10)
                    .ToListAsync(),
                SolicitacoesNovosFornecedores = await _db.Usuarios
                    .Where(u => u.Role == UserRole.Fornecedor && u.Status == UserStatus.Pendente)
                    .OrderByDescending(u => u.CriadoEm)
                    .Take(5)
                    .ToListAsync()
            };
            return View(vm);
        }

        // Usuários
        public async Task<IActionResult> Usuarios(string? tipo, string? busca, int page = 1)
        {
            const int porPagina = 20;
            var query = _db.Usuarios.Include(u => u.Empresa).AsQueryable();

            query = tipo switch
            {
                "Clientes"     => query.Where(u => u.Role == UserRole.Cliente),
                "Fornecedores" => query.Where(u => u.Role == UserRole.Fornecedor),
                "Admins"       => query.Where(u => u.Role == UserRole.Admin),
                _              => query.Where(u => u.Role != UserRole.Admin)
            };

            if (!string.IsNullOrWhiteSpace(busca))
                query = query.Where(u => u.NomeCompleto.Contains(busca) || u.Email.Contains(busca));

            var total    = await query.CountAsync();
            var usuarios = await query
                .OrderByDescending(u => u.CriadoEm)
                .Skip((page - 1) * porPagina)
                .Take(porPagina)
                .ToListAsync();

            ViewBag.Tipo       = tipo ?? "Todos";
            ViewBag.Busca      = busca;
            ViewBag.Page       = page;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)porPagina);
            ViewBag.Total      = total;
            return View(usuarios);
        }

        // Aceitar Fornecedor
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AceitarFornecedor(int id)
        {
            var u = await _db.Usuarios.FindAsync(id);
            if (u == null) return NotFound();

            u.Status = UserStatus.Ativo;
            await _db.SaveChangesAsync();

            // Mensagem automática de aprovação visível ao fornecedor no chat do suporte
            var msgAprov = new MensagemGrupo
            {
                Conteudo     = $"🎉 Parabéns, {u.NomeCompleto}! Sua conta foi aprovada como Fornecedor na MindSite. Seja bem-vindo(a) ao nosso time! Agora você já pode cadastrar seus serviços e começar a atender clientes pela plataforma. Qualquer dúvida, estamos à disposição! 😊",
                EhSistema    = false,
                RemetenteId  = UserId,
                ThreadUserId = u.Id
            };
            _db.MensagensGrupo.Add(msgAprov);
            await _db.SaveChangesAsync();

            var aprvPayload = new { id = msgAprov.Id, conteudo = msgAprov.Conteudo, remetenteId = msgAprov.RemetenteId,
                threadUserId = msgAprov.ThreadUserId, enviadaEm = msgAprov.EnviadaEm.ToLocalTime().ToString("HH:mm"),
                nomeRemetente = "Suporte MindSite", ehSistema = false };
            await _hub.Clients.Group("grupo-suporte").SendAsync("ReceiveGrupoMessage", aprvPayload);
            await _hub.Clients.Group($"user-{u.Id}").SendAsync("ReceiveGrupoMessage", aprvPayload);

            await _notif.CriarAsync(u.Id,
                "Conta aprovada!",
                url: "/Supplier/Chat?grupoSuporte=true");
            await _log.RegistrarAsync(UserId, $"Aprovou fornecedor {u.NomeCompleto}.");

            // Hot-reload: redireciona o fornecedor aprovado em tempo real
            await _hub.Clients.Group($"user-{u.Id}").SendAsync("ForceRedirect", "/Supplier/Index");

            TempData["Success"] = $"{u.NomeCompleto} aprovado como Fornecedor!";
            return RedirectToAction("Usuarios");
        }

        // Recusar/Reverter Fornecedor
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RecusarFornecedor(int id)
        {
            var u = await _db.Usuarios.Include(x => x.Empresa).FirstOrDefaultAsync(x => x.Id == id);
            if (u == null) return NotFound();

            u.Role   = UserRole.Cliente;
            u.Status = UserStatus.Ativo;
            await _db.SaveChangesAsync();
            await _notif.CriarAsync(u.Id, "Solicitação recusada.");
            await _log.RegistrarAsync(UserId, $"Recusou fornecedor {u.NomeCompleto}.");

            TempData["Info"] = $"Solicitação de {u.NomeCompleto} recusada.";
            return RedirectToAction("Usuarios");
        }

        // Bloquear/Desbloquear
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleBloqueio(int id)
        {
            var u = await _db.Usuarios.FindAsync(id);
            if (u == null) return NotFound();

            u.Status = u.Status == UserStatus.Bloqueado ? UserStatus.Ativo : UserStatus.Bloqueado;
            await _db.SaveChangesAsync();
            await _log.RegistrarAsync(UserId, $"{(u.Status == UserStatus.Bloqueado ? "Bloqueou" : "Desbloqueou")} {u.NomeCompleto}.");

            TempData["Success"] = u.Status == UserStatus.Bloqueado ? "Usuário bloqueado." : "Usuário desbloqueado.";
            return RedirectToAction("Usuarios");
        }

        // Alterar Status do Usuário
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AlterarStatusUsuario(int id, UserStatus status)
        {
            var u = await _db.Usuarios.FindAsync(id);
            if (u == null) return NotFound();
            u.Status = status;
            await _db.SaveChangesAsync();
            await _log.RegistrarAsync(UserId, $"Alterou status de {u.NomeCompleto} para {status}.");
            return Ok();
        }


        // Cadastrar Fornecedor
        [HttpGet]
        public IActionResult CadastrarFornecedor() => View();

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CadastrarFornecedor(CadastrarFornecedorViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            if (await _db.Usuarios.AnyAsync(u => u.Email == vm.Email.ToLower().Trim()))
            {
                ModelState.AddModelError("Email", "Este e-mail já está cadastrado.");
                return View(vm);
            }

            var u = new Usuario
            {
                NomeCompleto = vm.NomeCompleto.Trim(),
                Email        = vm.Email.ToLower().Trim(),
                Telefone     = vm.Telefone.Trim(),
                SenhaHash    = BCrypt.Net.BCrypt.HashPassword(vm.Senha),
                Role         = UserRole.Fornecedor,
                Status       = UserStatus.Ativo,
                LinkedInUrl    = vm.LinkedInUrl,
                WhatsAppNumero = vm.WhatsAppNumero,
                CPF            = vm.CPF,
                SobreVoce      = vm.SobreVoce
            };
            _db.Usuarios.Add(u);
            await _db.SaveChangesAsync();
            await _notif.CriarAsync(u.Id, "Bem-vindo à MindSite!", url: "/Supplier/Index");
            await _log.RegistrarAsync(UserId, $"Cadastrou fornecedor {u.NomeCompleto}.");

            TempData["Success"] = $"Fornecedor {u.NomeCompleto} cadastrado com sucesso!";
            return RedirectToAction("Usuarios", new { tipo = "Fornecedores" });
        }

        // API: detalhes de usuário (modal inline)
        [HttpGet]
        public async Task<IActionResult> GetUsuarioApi(int id)
        {
            var u = await _db.Usuarios.Include(x => x.Empresa).FirstOrDefaultAsync(x => x.Id == id);
            if (u == null) return NotFound();
            return Json(new
            {
                u.Id, u.NomeCompleto, u.Email, u.Telefone,
                role   = u.Role.ToString(),
                status = u.Status.ToString(),
                criadoEm = u.CriadoEm.ToLocalTime().ToString("dd/MM/yyyy"),
                u.LinkedInUrl, u.WhatsAppNumero, u.CPF, u.SobreVoce,
                empresa = u.Empresa == null ? null : new
                {
                    u.Empresa.NomeEmpresa, u.Empresa.CNPJ,
                    u.Empresa.Estado, u.Empresa.Cidade
                }
            });
        }

        // Detalhes do serviço ativo (modal de chat admin)
        [HttpGet]
        public async Task<IActionResult> GetServicoAtivoDetalhesAdmin(int usuarioId)
        {
            var u = await _db.Usuarios.FindAsync(usuarioId);
            if (u == null) return Json(new { ok = false });
            Servico? s = null;
            if (u.Role == UserRole.Cliente)
                s = await _db.Servicos
                    .Where(x => x.ClienteId == usuarioId && x.PagamentoInicialFeito
                             && x.Status != ServicoStatus.Cancelado && x.Status != ServicoStatus.Concluido)
                    .OrderByDescending(x => x.CriadoEm)
                    .FirstOrDefaultAsync();
            else if (u.Role == UserRole.Fornecedor)
                s = await _db.Servicos
                    .Where(x => x.FornecedorId == usuarioId && x.PagamentoInicialFeito
                             && x.Status != ServicoStatus.Cancelado && x.Status != ServicoStatus.Concluido)
                    .OrderByDescending(x => x.CriadoEm)
                    .FirstOrDefaultAsync();
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
                valor   = s.Valor.HasValue ? s.Valor.Value.ToString("N2") : null,
                entrega = s.DataEntrega.HasValue ? s.DataEntrega.Value.ToLocalTime().ToString("dd/MM/yyyy") : null,
                obs     = s.Observacoes });
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

        // Pedidos
        public async Task<IActionResult> Pedidos(int? mudancaId, int page = 1)
        {
            const int porPagina = 15;
            var query = _db.Servicos
                .Include(s => s.Cliente)
                .Include(s => s.Fornecedor)
                .Include(s => s.Propostas).ThenInclude(p => p.Fornecedor)
                .Include(s => s.Mudancas)
                .Where(s => s.Status != ServicoStatus.Concluido &&
                            s.Status != ServicoStatus.Cancelado &&
                            s.Status != ServicoStatus.Recusado);

            var total    = await query.CountAsync();
            var servicos = await query
                .OrderBy(s => s.CriadoEm)
                .Skip((page - 1) * porPagina)
                .Take(porPagina)
                .ToListAsync();

            ViewBag.MudancaId   = mudancaId;
            ViewBag.Page        = page;
            ViewBag.TotalPages  = (int)Math.Ceiling(total / (double)porPagina);
            return View(servicos);
        }

        // Definir Orçamento do Projeto
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DefinirOrcamento(int id, decimal orcamento)
        {
            var s = await _db.Servicos.FindAsync(id);
            if (s == null) return NotFound();
            s.OrcamentoCliente = orcamento;
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Orçamento de R$ {orcamento:N2} definido com sucesso.";
            return RedirectToAction("Pedidos");
        }

        // Adicionar Valor à Mudança
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarValorMudanca(int mudancaId, decimal? valorAdicional)
        {
            var m = await _db.SolicitacoesMudanca
                .Include(x => x.Servico)
                .FirstOrDefaultAsync(x => x.Id == mudancaId);
            if (m == null) return NotFound();

            m.ValorAdicional = valorAdicional;

            if (m.Servico != null)
            {
                // Aplica as mudanças do diff ao serviço
                try
                {
                    var campos = JsonSerializer.Deserialize<List<JsonElement>>(m.Descricao);
                    if (campos != null)
                    {
                        foreach (var c in campos)
                        {
                            if (!c.TryGetProperty("campo", out var campoEl)) continue;
                            var campo = campoEl.GetString();
                            var para  = c.TryGetProperty("para", out var paraEl) ? paraEl.GetString() : null;
                            if (campo == "Nome do Projeto") m.Servico.NomeProjeto = para;
                            if (campo == "Observações")     m.Servico.Observacoes = para;
                        }
                    }
                }
                catch { /* diff inválido: ignorar */ }

                if (valorAdicional.HasValue && valorAdicional > 0)
                    m.Servico.Valor = (m.Servico.Valor ?? 0) + valorAdicional.Value;

                m.Servico.Status       = ServicoStatus.Ativo;
                m.Servico.AtualizadoEm = DateTime.UtcNow;

                // Auto-mensagem via chat do fornecedor
                if (m.Servico.FornecedorId.HasValue)
                {
                    var desc = valorAdicional.HasValue && valorAdicional > 0
                        ? $"A MindSite adicionou R$ {valorAdicional.Value:N2} ao valor do projeto em razão da mudança solicitada. O projeto foi atualizado e está em andamento."
                        : "A MindSite processou sua solicitação de mudança sem custo adicional. O projeto foi atualizado e está em andamento.";

                    var msgJson = JsonSerializer.Serialize(new { _t = "V", r = desc, v = valorAdicional?.ToString("N2") });
                    var msg     = new Mensagem
                    {
                        RemetenteId    = m.Servico.FornecedorId.Value,
                        DestinatarioId = m.Servico.ClienteId,
                        Conteudo       = msgJson
                    };
                    _db.Mensagens.Add(msg);
                    await _db.SaveChangesAsync();

                    var fornecedor = await _db.Usuarios.FindAsync(m.Servico.FornecedorId.Value);
                    var payload = new { id = msg.Id, conteudo = msgJson, remetenteId = msg.RemetenteId, destinatarioId = msg.DestinatarioId, enviadaEm = msg.EnviadaEm.ToLocalTime().ToString("HH:mm"), lida = false, nomeRemetente = fornecedor?.NomeCompleto ?? "" };
                    await _hub.Clients.Group($"user-{m.Servico.FornecedorId.Value}").SendAsync("ReceiveMessage", payload);
                    await _hub.Clients.Group($"user-{m.Servico.ClienteId}").SendAsync("ReceiveMessage", payload);

                    var notifMsg = valorAdicional.HasValue && valorAdicional > 0
                        ? "Cobrança adicional."
                        : "Mudança aprovada.";
                    await _notif.CriarAsync(m.Servico.ClienteId, notifMsg,
                        url: $"/Client/Chat?contatoId={m.Servico.FornecedorId.Value}", servicoId: m.ServicoId);
                }
            }

            await _db.SaveChangesAsync();
            await _log.RegistrarAsync(UserId, $"Processou mudança #{mudancaId} com valor adicional R$ {valorAdicional:N2}.");
            TempData["Success"] = "Mudança processada com sucesso!";
            return RedirectToAction("Pedidos");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EnviarParaFornecedores(int id, decimal taxaPlataforma)
        {
            var s = await _db.Servicos.FindAsync(id);
            if (s == null || s.Status != ServicoStatus.Pendente) return NotFound();

            if (!s.OrcamentoCliente.HasValue)
            {
                TempData["Erro"] = "Defina o orçamento do projeto antes de enviar para fornecedores.";
                return RedirectToAction("Pedidos");
            }

            s.Status         = ServicoStatus.AguardandoPropostas;
            s.TaxaPlataforma = taxaPlataforma;
            s.AtualizadoEm   = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            var fornecedores = await _db.Usuarios
                .Where(u => u.Role == UserRole.Fornecedor && u.Status == UserStatus.Ativo)
                .ToListAsync();
            foreach (var f in fornecedores)
                await _notif.CriarAsync(f.Id, "Nova solicitação!", url: "/Supplier/Servicos", servicoId: id);

            await _log.RegistrarAsync(UserId, $"Enviou serviço #{id} para fornecedores com taxa {taxaPlataforma}%.");
            TempData["Success"] = "Pedido enviado aos fornecedores!";
            return RedirectToAction("Pedidos");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> FecharPropostaUnica(int id)
        {
            var p = await _db.Propostas.Include(x => x.Servico).FirstOrDefaultAsync(x => x.Id == id);
            if (p == null) return NotFound();

            var s = p.Servico!;
            if (s.Status != ServicoStatus.AguardandoPropostas && s.Status != ServicoStatus.AguardandoEscolha)
            {
                TempData["Erro"] = "Não é possível fechar esta proposta no status atual.";
                return RedirectToAction("Pedidos");
            }
            if (p.FechadaPeloAdmin)
            {
                TempData["Info"] = "Esta proposta já foi fechada.";
                return RedirectToAction("Pedidos");
            }

            p.FechadaPeloAdmin = true;

            if (s.Status == ServicoStatus.AguardandoPropostas)
            {
                s.Status       = ServicoStatus.AguardandoEscolha;
                s.AtualizadoEm = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            await _notif.CriarAsync(s.ClienteId,
                "Proposta disponível!",
                url: "/Client/MeusServicos", servicoId: s.Id);

            await _log.RegistrarAsync(UserId, $"Fechou proposta #{id} para serviço #{s.Id}.");
            TempData["Success"] = "Proposta fechada e enviada ao cliente.";
            return RedirectToAction("Pedidos");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmarPagamentoInicial(int id)
        {
            var s = await _db.Servicos.FirstOrDefaultAsync(x => x.Id == id && x.Status == ServicoStatus.Ativo && !x.PagamentoInicialFeito);
            if (s == null) return NotFound();

            s.PagamentoInicialFeito = true;
            s.AtualizadoEm          = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await _notif.CriarAsync(s.ClienteId,
                "Pagamento confirmado.",
                url: s.FornecedorId.HasValue ? $"/Client/Chat?contatoId={s.FornecedorId}" : "/Client/MeusServicos",
                servicoId: id);
            if (s.FornecedorId.HasValue)
                await _notif.CriarAsync(s.FornecedorId.Value,
                    "Pagamento recebido!",
                    url: $"/Supplier/Chat?contatoId={s.ClienteId}", servicoId: id);

            await _log.RegistrarAsync(UserId, $"Confirmou pagamento inicial do serviço #{id}.");
            TempData["Success"] = "Pagamento inicial confirmado!";
            return RedirectToAction("Pedidos");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmarPagamentoFinal(int id)
        {
            var s = await _db.Servicos.FirstOrDefaultAsync(x => x.Id == id && x.Status == ServicoStatus.AguardandoPagamentoFinal);
            if (s == null) return NotFound();

            s.PagamentoFinalFeito = true;
            s.Status              = ServicoStatus.Concluido;
            s.AtualizadoEm        = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await _notif.CriarAsync(s.ClienteId,
                "Projeto concluído!",
                url: "/Client/MeusServicos", servicoId: id);
            if (s.FornecedorId.HasValue)
                await _notif.CriarAsync(s.FornecedorId.Value,
                    "Pagamento final confirmado.",
                    url: "/Supplier/Servicos?filtro=meus", servicoId: id);

            await _log.RegistrarAsync(UserId, $"Confirmou pagamento final do serviço #{id}.");
            TempData["Success"] = "Pagamento final confirmado! Projeto concluído.";
            return RedirectToAction("Pedidos");
        }

        // Mudanças → redirecionado para Pedidos
        public IActionResult Mudancas() => RedirectToAction("Pedidos");

        // Chat Admin
        public async Task<IActionResult> Chat(int? contatoId, bool grupoSuporte = false, int? threadUserId = null)
        {
            var uid = UserId;
            var eu  = await _db.Usuarios.FindAsync(uid);

            var contatosPinados = await _db.Usuarios
                .Where(u => u.Role == UserRole.Admin && u.Id != uid)
                .OrderBy(u => u.NomeCompleto)
                .ToListAsync();

            var pinadosIds = contatosPinados.Select(c => c.Id).ToHashSet();

            var contatosIds = await _db.Mensagens
                .Where(m => m.RemetenteId == uid || m.DestinatarioId == uid)
                .Select(m => m.RemetenteId == uid ? m.DestinatarioId : m.RemetenteId)
                .Distinct().ToListAsync();

            var conversas = new List<ConversaItem>();
            foreach (var cid in contatosIds.Where(id => !pinadosIds.Contains(id)))
            {
                var contato = await _db.Usuarios.FindAsync(cid);
                if (contato == null) continue;
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

            // Adiciona pinados ao conversas para contagem de não-lidas
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
                    Contato        = p,
                    UltimaMensagem = ult?.Conteudo ?? "",
                    Quando         = ult?.EnviadaEm,
                    NaoLidas       = naoLidas,
                    Pinado         = true
                });
            }

            // Grupo Suporte
            var ultMsgGrupo = await _db.MensagensGrupo
                .OrderByDescending(m => m.EnviadaEm)
                .FirstOrDefaultAsync();
            var grupoUltimaMensagem = ultMsgGrupo?.Conteudo ?? "";
            DateTime? grupoUltimaData = ultMsgGrupo?.EnviadaEm;

            var grupoThreads  = new List<GrupoThread>();
            List<MensagemGrupo> grupoMensagens = new();
            Usuario? grupoThreadUser = null;

            // Sempre carrega threads para a sidebar
            var allThreadIds = await _db.MensagensGrupo
                .Select(m => m.ThreadUserId)
                .Distinct()
                .ToListAsync();
            foreach (var tid in allThreadIds)
            {
                var tUser = await _db.Usuarios.FindAsync(tid);
                if (tUser == null) continue;
                var ultMsg = await _db.MensagensGrupo
                    .Where(m => m.ThreadUserId == tid)
                    .OrderByDescending(m => m.EnviadaEm)
                    .FirstOrDefaultAsync();
                grupoThreads.Add(new GrupoThread
                {
                    Usuario        = tUser,
                    UltimaMensagem = ultMsg?.Conteudo ?? "",
                    Quando         = ultMsg?.EnviadaEm
                });
            }
            grupoThreads = grupoThreads.OrderByDescending(t => t.Quando).ToList();

            // Carrega mensagens da thread selecionada
            if (grupoSuporte && threadUserId.HasValue)
            {
                grupoThreadUser = await _db.Usuarios.FindAsync(threadUserId.Value);
                grupoMensagens  = await _db.MensagensGrupo
                    .Include(m => m.Remetente)
                    .Where(m => m.ThreadUserId == threadUserId.Value)
                    .OrderBy(m => m.EnviadaEm)
                    .ToListAsync();
            }

            List<Mensagem> mensagens = new();
            Usuario? contatoSel = null;
            if (contatoId.HasValue)
            {
                contatoSel = await _db.Usuarios.FindAsync(contatoId.Value);
                mensagens  = await _db.Mensagens
                    .Include(m => m.Remetente)
                    .Where(m => (m.RemetenteId == uid && m.DestinatarioId == contatoId) ||
                                (m.RemetenteId == contatoId && m.DestinatarioId == uid))
                    .OrderBy(m => m.EnviadaEm).ToListAsync();
                foreach (var mm in mensagens.Where(m => m.DestinatarioId == uid && !m.Lida)) mm.Lida = true;
                await _db.SaveChangesAsync();
            }

            return View(new ChatViewModel
            {
                UsuarioAtual         = eu ?? new Usuario(),
                Conversas            = conversas.OrderByDescending(c => c.Pinado).ThenByDescending(c => c.Quando).ToList(),
                ContatoSelecionadoId = contatoId,
                ContatoSelecionado   = contatoSel,
                Mensagens            = mensagens,
                ContatosPinados      = contatosPinados,
                IsGrupoSuporte       = grupoSuporte,
                GrupoMensagens       = grupoMensagens,
                GrupoUltimaMensagem  = grupoUltimaMensagem,
                GrupoUltimaData      = grupoUltimaData,
                GrupoThreads         = grupoThreads,
                GrupoThreadUserId    = threadUserId,
                GrupoThreadUser      = grupoThreadUser
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EnviarMensagem(EnviarMensagemViewModel vm)
        {
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
    }
}
