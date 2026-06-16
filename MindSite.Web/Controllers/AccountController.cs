using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google; // Adicionado para o GoogleDefaults
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MindSite.Data;
using MindSite.Hubs;
using MindSite.Entities;
using MindSite.Enums;
using Microsoft.Extensions.DependencyInjection;
using MindSite.Services;
using System.Security.Claims;
using MindSite.Interfaces;
using MindSite.ViewModels;

namespace MindSite.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _db;
        private readonly NotificacaoService _notif;
        private readonly LogService _log;
        private readonly IEmailService _email;
        private readonly IHubContext<ChatHub> _hub;
        private readonly IWebHostEnvironment _env;

        public AccountController(AppDbContext db, NotificacaoService notif, LogService log, IEmailService email, IHubContext<ChatHub> hub, IWebHostEnvironment env)
        {
            _db = db;
            _notif = notif;
            _log = log;
            _email = email;
            _hub = hub;
            _env = env;
        }

        // Login Local
        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToRole();
            return View("Auth");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                TempData["Erro"] = "Preencha todos os campos.";
                return View("Auth");
            }

            var usuario = await _db.Usuarios
                .FirstOrDefaultAsync(u => u.Email == vm.Email.ToLower().Trim());

            if (usuario == null || !BCrypt.Net.BCrypt.Verify(vm.Senha, usuario.SenhaHash))
            {
                TempData["Erro"] = "E-mail ou senha incorretos.";
                return View("Auth");
            }

            return await ProcessarLoginUsuarioAsync(usuario);
        }

        // Autenticação Google
        [HttpGet]
        public IActionResult LoginGoogle()
        {
            // Propriedades para redirecionar de volta para a action de Callback
            var properties = new AuthenticationProperties
            {
                RedirectUri = Url.Action("GoogleCallback")
            };

            // Dispara o desafio do Google configurado no Program.cs
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [HttpGet]
        public async Task<IActionResult> GoogleCallback()
        {
            // Lê o resultado da autenticação externa obtida pelo cookie temporário do Google
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!result.Succeeded)
            {
                TempData["Erro"] = "Falha na autenticação com o Google.";
                return RedirectToAction("Login");
            }

            var claims = result.Principal.Identities.FirstOrDefault()?.Claims;
            var email = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value?.ToLower().Trim();
            var nome = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value?.Trim();

            if (string.IsNullOrEmpty(email))
            {
                TempData["Erro"] = "Não foi possível obter o e-mail da sua conta Google.";
                return RedirectToAction("Login");
            }

            // Procura o usuário local baseado no e-mail retornado pelo Google
            var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Email == email);

            if (usuario == null)
            {
                // Se o usuário não existir, cria um novo automaticamente como Cliente
                usuario = new Usuario
                {
                    NomeCompleto = nome ?? "Usuário Google",
                    Email = email,
                    Telefone = "—", // Placeholder, já que o Google não fornece por padrão
                    SenhaHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N")), // Senha aleatória forte inútil
                    Role = UserRole.Cliente,
                    Status = UserStatus.Ativo,
                    AceitouContratoClienteEm = DateTime.UtcNow
                };

                _db.Usuarios.Add(usuario);
                await _db.SaveChangesAsync();
                await _log.RegistrarAsync(usuario.Id, "Conta criada via Autenticação Google.");
            }

            return await ProcessarLoginUsuarioAsync(usuario);
        }

        // Helper centralizado para processar as regras de negócio pós-login (Google ou Local)
        private async Task<IActionResult> ProcessarLoginUsuarioAsync(Usuario usuario)
        {
            if (usuario.Status == UserStatus.Bloqueado)
            {
                TempData["Erro"] = "Sua conta foi bloqueada. Entre em contato com o suporte.";
                return View("Auth");
            }

            if (usuario.Role == UserRole.Fornecedor && usuario.Status == UserStatus.Inativo)
            {
                TempData["Erro"] = "Sua conta de fornecedor foi inativada. Entre em contato com o suporte.";
                return View("Auth");
            }

            // Atualiza online + último acesso
            var primeiroLogin = usuario.UltimoAcesso == null && usuario.Role == UserRole.Cliente;
            usuario.IsOnline = true;
            usuario.UltimoAcesso = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await _log.RegistrarAsync(usuario.Id, "Realizou login no sistema.");

            if (primeiroLogin)
            {
                var admin = await _db.Usuarios.FirstOrDefaultAsync(a => a.Role == UserRole.Admin);
                if (admin != null)
                {
                    var msgBv = new MensagemGrupo
                    {
                        Conteudo = $"Olá, {usuario.NomeCompleto}! 👋 Seja bem-vindo(a) à MindSite! Ficamos felizes em ter você por aqui. Se precisar de qualquer ajuda, é só mandar uma mensagem por aqui mesmo. 😊",
                        EhSistema = false,
                        RemetenteId = admin.Id,
                        ThreadUserId = usuario.Id
                    };
                    _db.MensagensGrupo.Add(msgBv);
                    await _db.SaveChangesAsync();

                    var bvPayload = new
                    {
                        id = msgBv.Id,
                        conteudo = msgBv.Conteudo,
                        remetenteId = msgBv.RemetenteId,
                        threadUserId = msgBv.ThreadUserId,
                        enviadaEm = msgBv.EnviadaEm.ToLocalTime().ToString("HH:mm"),
                        nomeRemetente = "Suporte MindSite",
                        ehSistema = false
                    };
                    await _hub.Clients.Group("grupo-suporte").SendAsync("ReceiveGrupoMessage", bvPayload);
                    await _hub.Clients.Group($"user-{usuario.Id}").SendAsync("ReceiveGrupoMessage", bvPayload);

                    await _notif.CriarAsync(usuario.Id, "Bem-vindo à MindSite!", url: "/Client/Chat?grupoSuporte=true");
                }
            }

            await SignInAsync(usuario);
            return RedirectToRole(usuario.Role);
        }

        // Cadastro
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Cadastro(CadastroViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErroCadastro"] = string.Join(" | ", ModelState.Values
                    .SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return View("Auth");
            }

            if (await _db.Usuarios.AnyAsync(u => u.Email == vm.Email.ToLower().Trim()))
            {
                TempData["ErroCadastro"] = "Este e-mail já está cadastrado.";
                return View("Auth");
            }

            var usuario = new Usuario
            {
                NomeCompleto = vm.NomeCompleto.Trim(),
                Email = vm.Email.ToLower().Trim(),
                Telefone = vm.Telefone.Trim(),
                SenhaHash = BCrypt.Net.BCrypt.HashPassword(vm.Senha),
                Role = UserRole.Cliente,
                Status = UserStatus.Ativo,
                AceitouContratoClienteEm = vm.AceitouContrato ? DateTime.UtcNow : null
            };
            _db.Usuarios.Add(usuario);
            await _db.SaveChangesAsync();
            await _log.RegistrarAsync(usuario.Id, "Conta criada.");

            TempData["Success"] = "Cadastro realizado! Faça login.";
            return View("Auth");
        }

        // Logout
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var id = int.Parse(User.FindFirstValue("UserId") ?? "0");
            if (id > 0)
            {
                var u = await _db.Usuarios.FindAsync(id);
                if (u != null) { u.IsOnline = false; await _db.SaveChangesAsync(); }
            }
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        // Perfil
        [Authorize]
        public async Task<IActionResult> Perfil()
        {
            var id = int.Parse(User.FindFirstValue("UserId")!);
            var u = await _db.Usuarios.Include(u => u.Empresa).FirstOrDefaultAsync(u => u.Id == id);
            if (u == null) return RedirectToAction("Logout");
            var vm = new PerfilPageViewModel { Usuario = u };
            if (u.Role == UserRole.Cliente)
            {
                var servicos = await _db.Servicos
                    .Where(s => s.ClienteId == id && s.PagamentoInicialFeito)
                    .Include(s => s.Fornecedor)
                    .OrderByDescending(s => s.CriadoEm)
                    .ToListAsync();
                vm.MeusProjetos = servicos.Select(s => new ProjetoPerfilItem
                {
                    NomeProjeto = s.NomeProjeto ?? $"Serviço #{s.Id}",
                    NomeFornecedor = s.Fornecedor?.NomeCompleto ?? "—",
                    Inicio = s.PagamentoInicialEm ?? s.CriadoEm,
                    Status = s.Status switch
                    {
                        ServicoStatus.Ativo => "Em andamento",
                        ServicoStatus.Concluido => "Concluído",
                        ServicoStatus.Cancelado => "Cancelado",
                        ServicoStatus.AguardandoPagamentoFinal => "Ag. Pagamento",
                        _ => s.Status.ToString()
                    },
                    StatusClass = s.Status switch
                    {
                        ServicoStatus.Ativo => "badge-blue",
                        ServicoStatus.Concluido => "badge-green",
                        ServicoStatus.Cancelado => "badge-red",
                        _ => "badge-amber"
                    }
                }).ToList();
            }
            else if (u.Role == UserRole.Fornecedor)
            {
                vm.TotalServicos = await _db.Servicos.CountAsync(s => s.FornecedorId == id && s.PagamentoInicialFeito);
                vm.TotalAvaliacoes = await _db.Avaliacoes.CountAsync(a => a.FornecedorId == id);
                vm.MediaEstrelas = vm.TotalAvaliacoes > 0
                    ? await _db.Avaliacoes.Where(a => a.FornecedorId == id).AverageAsync(a => (double)a.Estrelas)
                    : 0;
            }
            return View(vm);
        }

        [Authorize, HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarDados(EditarDadosViewModel vm)
        {
            var id = int.Parse(User.FindFirstValue("UserId")!);
            var u = await _db.Usuarios.Include(uu => uu.Empresa).FirstOrDefaultAsync(uu => uu.Id == id);
            if (u == null) return RedirectToAction("Logout");
            u.NomeCompleto = vm.NomeCompleto.Trim();
            u.Telefone = vm.Telefone?.Trim() ?? u.Telefone;
            u.Endereco = vm.Endereco?.Trim();
            if (u.Role == UserRole.Fornecedor)
            {
                u.LinkedInUrl = vm.LinkedInUrl;
                u.WhatsAppNumero = vm.WhatsAppNumero;
                u.CPF = vm.CPF;
                u.SobreVoce = vm.SobreVoce;
            }
            await _db.SaveChangesAsync();
            await _log.RegistrarAsync(u.Id, "Atualizou dados pessoais.");
            TempData["Success"] = "Dados updated!";
            return RedirectToAction("Perfil");
        }

        [Authorize, HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> TrocarSenha(TrocarSenhaViewModel vm)
        {
            var id = int.Parse(User.FindFirstValue("UserId")!);
            var u = await _db.Usuarios.FindAsync(id);
            if (u == null) return RedirectToAction("Logout");
            if (!BCrypt.Net.BCrypt.Verify(vm.SenhaAtual, u.SenhaHash))
            {
                TempData["ErroPerfil"] = "Senha atual incorreta.";
                return RedirectToAction("Perfil");
            }
            if (vm.NovaSenha != vm.ConfirmarSenha)
            {
                TempData["ErroPerfil"] = "Senhas não conferem.";
                return RedirectToAction("Perfil");
            }
            u.SenhaHash = BCrypt.Net.BCrypt.HashPassword(vm.NovaSenha);
            await _db.SaveChangesAsync();
            await _log.RegistrarAsync(u.Id, "Alterou a senha.");
            TempData["Success"] = "Senha alterada com sucesso!";
            return RedirectToAction("Perfil");
        }

        [Authorize, HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadFotoPerfil(IFormFile foto)
        {
            if (foto == null || foto.Length == 0) return Json(new { ok = false, erro = "Arquivo inválido." });
            var id = int.Parse(User.FindFirstValue("UserId")!);
            var u = await _db.Usuarios.FindAsync(id);
            if (u == null) return Json(new { ok = false });
            var uploadsPath = Path.Combine(_env.WebRootPath, "uploads", "perfil");
            Directory.CreateDirectory(uploadsPath);
            var ext = Path.GetExtension(foto.FileName).ToLower();
            var fileName = $"perfil_{id}{ext}";
            var filePath = Path.Combine(uploadsPath, fileName);
            await using var stream = new FileStream(filePath, FileMode.Create);
            await foto.CopyToAsync(stream);
            u.FotoPerfilUrl = $"/uploads/perfil/{fileName}?v={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            await _db.SaveChangesAsync();
            return Json(new { ok = true, url = u.FotoPerfilUrl });
        }

        [Authorize, HttpPost]
        public async Task<IActionResult> SalvarPreferencias(bool notificacoesEmail, bool notificacoesChat)
        {
            var id = int.Parse(User.FindFirstValue("UserId")!);
            var u = await _db.Usuarios.FindAsync(id);
            if (u != null)
            {
                u.NotificacoesEmail = notificacoesEmail;
                u.NotificacoesChat = notificacoesChat;
                await _db.SaveChangesAsync();
            }
            return Ok();
        }

        // Alterar Status Online
        [Authorize, HttpPost]
        public async Task<IActionResult> AlterarStatus(bool online)
        {
            var id = int.Parse(User.FindFirstValue("UserId")!);
            var u = await _db.Usuarios.FindAsync(id);
            if (u != null) { u.IsOnline = online; await _db.SaveChangesAsync(); }
            return Ok();
        }

        [Authorize, HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarPerfil(PerfilViewModel vm)
        {
            var id = int.Parse(User.FindFirstValue("UserId")!);
            var u = await _db.Usuarios.Include(uu => uu.Empresa).FirstOrDefaultAsync(uu => uu.Id == id);
            if (u == null) return RedirectToAction("Logout");
            u.NomeCompleto = vm.NomeCompleto.Trim();
            u.Telefone = vm.Telefone.Trim();
            if (!string.IsNullOrWhiteSpace(vm.NovaSenha))
            {
                if (!BCrypt.Net.BCrypt.Verify(vm.SenhaAtual, u.SenhaHash))
                { TempData["ErroPerfil"] = "Senha atual incorreta."; return RedirectToAction("Perfil"); }
                u.SenhaHash = BCrypt.Net.BCrypt.HashPassword(vm.NovaSenha);
            }
            if (u.Role == UserRole.Fornecedor)
            { u.LinkedInUrl = vm.LinkedInUrl; u.WhatsAppNumero = vm.WhatsAppNumero; u.CPF = vm.CPF; u.SobreVoce = vm.SobreVoce; }
            await _db.SaveChangesAsync();
            TempData["Success"] = "Perfil atualizado!";
            return RedirectToAction("Perfil");
        }

        // Virar Fornecedor
        [Authorize(Roles = "Cliente")]
        public IActionResult VirarFornecedor() => View();

        [Authorize(Roles = "Cliente"), HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> VirarFornecedor(VirarFornecedorViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var id = int.Parse(User.FindFirstValue("UserId")!);
            var u = await _db.Usuarios.Include(uu => uu.Empresa).FirstOrDefaultAsync(uu => uu.Id == id);
            if (u == null) return RedirectToAction("Logout");

            u.Role = UserRole.Fornecedor;
            u.Status = UserStatus.Pendente;
            u.LinkedInUrl = vm.LinkedInUrl;
            u.WhatsAppNumero = vm.WhatsAppNumero;
            u.CPF = vm.CPF;
            u.SobreVoce = vm.SobreVoce;
            u.AceitouContratoFornecedorEm = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            var msgForn = new MensagemGrupo
            {
                Conteudo = $"🚀 {u.NomeCompleto} ({u.Email}) solicitou se tornar Fornecedor e está aguardando aprovação.",
                EhSistema = true,
                RemetenteId = u.Id,
                ThreadUserId = u.Id
            };
            _db.MensagensGrupo.Add(msgForn);
            await _db.SaveChangesAsync();

            var fornPayload = new
            {
                id = msgForn.Id,
                conteudo = msgForn.Conteudo,
                remetenteId = msgForn.RemetenteId,
                threadUserId = msgForn.ThreadUserId,
                enviadaEm = msgForn.EnviadaEm.ToLocalTime().ToString("HH:mm"),
                nomeRemetente = u.NomeCompleto,
                ehSistema = true
            };
            await _hub.Clients.Group("grupo-suporte").SendAsync("ReceiveGrupoMessage", fornPayload);

            var admins = await _db.Usuarios.Where(a => a.Role == UserRole.Admin).ToListAsync();
            foreach (var admin in admins)
                await _notif.CriarAsync(admin.Id, "Solicitação de fornecedor.", url: $"/Admin/Usuarios?tipo=Fornecedor&selecionado={u.Id}");

            await _log.RegistrarAsync(u.Id, "Solicitou virar fornecedor.");
            await SignInAsync(u);

            TempData["Success"] = "Solicitação enviada! Aguarde aprovação do admin.";
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult RecuperarSenha()
        {
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RecuperarSenha(string email)
        {
            var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Email == email.ToLower().Trim());

            if (usuario == null)
            {
                TempData["Erro"] = "Nenhuma conta encontrada com este e-mail.";
                return RedirectToAction("RecuperarSenha"); // Corrigido para persistir os alertas via Redirect
            }

            usuario.TokenResetSenha = Guid.NewGuid().ToString("N");
            usuario.TokenResetExpira = DateTime.UtcNow.AddHours(1);
            await _db.SaveChangesAsync();

            var link = Url.Action("RedefinirSenha", "Account", new { token = usuario.TokenResetSenha }, Request.Scheme);
            var corpo = $@"
        <div style='font-family:sans-serif;max-width:480px;margin:0 auto'>
            <h2 style='color:#6B4117'>Redefinição de senha — MindSite</h2>
            <p>Recebemos uma solicitação para redefinir a senha da conta <strong>{usuario.Email}</strong>.</p>
            <p>Clique no botão abaixo para criar uma nova senha. O link expira em <strong>1 hora</strong>.</p>
            <a href='{link}' style='display:inline-block;padding:12px 24px;background:#6B4117;color:#fff;text-decoration:none;border-radius:8px;font-weight:bold;margin:16px 0'>
                Redefinir minha senha
            </a>
            <p style='color:#888;font-size:12px'>Se você não solicitou isso, ignore este e-mail.</p>
        </div>";

            try
            {
                await _email.EnviarAsync(usuario.Email, "Redefinição de senha — MindSite", corpo);
                TempData["Success"] = "E-mail de recuperação enviado! Verifique sua caixa de entrada.";
            }
            catch (Exception)
            {
                if (_env.IsDevelopment())
                {
                    TempData["Success"] = "Instruções geradas localmente."; // Preenche o Success para abrir a caixinha verde na view
                    TempData["ResetLink"] = link;
                }
                else
                {
                    TempData["Erro"] = "Ocorreu um problema ao tentar enviar o e-mail de recuperação. Tente novamente mais tarde.";
                }
            }

            return RedirectToAction("RecuperarSenha"); // Redireciona para o GET de forma limpa e exibe as notificações!
        }
        // Redefinir Senha
        [HttpGet]
        public async Task<IActionResult> RedefinirSenha(string token)
        {
            var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.TokenResetSenha == token && u.TokenResetExpira > DateTime.UtcNow);
            if (usuario == null)
            {
                TempData["Erro"] = "Link inválido ou expirado.";
                return RedirectToAction("RecuperarSenha");
            }
            ViewBag.Token = token;
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RedefinirSenha(string token, string novaSenha, string confirmarSenha)
        {
            if (novaSenha != confirmarSenha)
            {
                TempData["Erro"] = "As senhas não coincidem.";
                ViewBag.Token = token;
                return View();
            }

            var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.TokenResetSenha == token && u.TokenResetExpira > DateTime.UtcNow);
            if (usuario == null)
            {
                TempData["Erro"] = "Link inválido ou expirado.";
                return RedirectToAction("RecuperarSenha");
            }

            usuario.SenhaHash = BCrypt.Net.BCrypt.HashPassword(novaSenha);
            usuario.TokenResetSenha = null;
            usuario.TokenResetExpira = null;
            await _db.SaveChangesAsync();
            await _log.RegistrarAsync(usuario.Id, "Redefiniu a senha via recuperação.");

            TempData["Success"] = "Senha redefinida com sucesso! Faça login.";
            return RedirectToAction("Login");
        }

        // Notificações
        [Authorize, HttpGet]
        public async Task<IActionResult> Notificacoes()
        {
            var uid = int.Parse(User.FindFirstValue("UserId")!);
            var list = await _db.Notificacoes
                .Where(n => n.UsuarioId == uid && !n.Lida)
                .OrderByDescending(n => n.CriadaEm)
                .Take(15)
                .Select(n => new { n.Id, n.Titulo, n.Corpo, n.Url, n.CriadaEm, n.Contador })
                .ToListAsync();
            return Json(list);
        }

        [Authorize, HttpPost]
        public async Task<IActionResult> DismissNotificacao(int id)
        {
            var uid = int.Parse(User.FindFirstValue("UserId")!);
            var n = await _db.Notificacoes.FirstOrDefaultAsync(n => n.Id == id && n.UsuarioId == uid);
            if (n != null) { _db.Notificacoes.Remove(n); await _db.SaveChangesAsync(); }
            return Ok();
        }

        // Deletar Conta
        [Authorize, HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConta()
        {
            var id = int.Parse(User.FindFirstValue("UserId")!);
            var u = await _db.Usuarios.Include(uu => uu.Empresa).FirstOrDefaultAsync(uu => uu.Id == id);
            if (u == null) return RedirectToAction("Logout");

            _db.Usuarios.Remove(u);
            await _db.SaveChangesAsync();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["Success"] = "Sua conta foi removida com sucesso.";
            return RedirectToAction("Login");
        }

        public IActionResult AcessoNegado() => View();

        // Helpers privados
        private async Task SignInAsync(Usuario u)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name,  u.NomeCompleto),
                new(ClaimTypes.Email, u.Email),
                new(ClaimTypes.Role,  u.Role.ToString()),
                new("UserId",         u.Id.ToString()),
                new("Status",         u.Status.ToString())
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        }

        private IActionResult RedirectToRole(UserRole? role = null)
        {
            role ??= Enum.Parse<UserRole>(User.FindFirstValue(ClaimTypes.Role) ?? "Cliente");
            return role switch
            {
                UserRole.Admin => RedirectToAction("Index", "Admin"),
                UserRole.Fornecedor => RedirectToAction("Index", "Supplier"),
                _ => RedirectToAction("Index", "Client")
            };
        }
    }
}