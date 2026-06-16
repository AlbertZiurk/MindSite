using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MindSite.Data;
using MindSite.Entities;
using MindSite.Enums;
using MindSite.Services;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace MindSite.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        // userId → conjunto de connectionIds (suporte a múltiplas abas)
        private static readonly ConcurrentDictionary<int, HashSet<string>> _connections = new();

        // userId → contatoId que está visualizando (-1 = grupo suporte)
        private static readonly ConcurrentDictionary<int, int> _activeChatContext = new();

        private readonly AppDbContext       _db;
        private readonly NotificacaoService _notif;

        public ChatHub(AppDbContext db, NotificacaoService notif)
        {
            _db    = db;
            _notif = notif;
        }

        private int? GetUserId()
        {
            var v = Context.User?.FindFirstValue("UserId");
            return v != null ? int.Parse(v) : null;
        }

        public override async Task OnConnectedAsync()
        {
            var uid = GetUserId();
            if (uid.HasValue)
            {
                var conns = _connections.GetOrAdd(uid.Value, _ => new HashSet<string>());
                lock (conns) conns.Add(Context.ConnectionId);

                await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{uid.Value}");

                // Admins entram no grupo SignalR do suporte
                if (Context.User?.IsInRole("Admin") == true)
                    await Groups.AddToGroupAsync(Context.ConnectionId, "grupo-suporte");

                var u = await _db.Usuarios.FindAsync(uid.Value);
                if (u != null && !u.IsOnline)
                {
                    u.IsOnline = true;
                    await _db.SaveChangesAsync();
                }
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? ex)
        {
            var uid = GetUserId();
            if (uid.HasValue && _connections.TryGetValue(uid.Value, out var conns))
            {
                bool empty;
                lock (conns)
                {
                    conns.Remove(Context.ConnectionId);
                    empty = conns.Count == 0;
                }
                if (empty)
                {
                    _connections.TryRemove(uid.Value, out _);
                    _activeChatContext.TryRemove(uid.Value, out _);
                    var u = await _db.Usuarios.FindAsync(uid.Value);
                    if (u != null)
                    {
                        u.IsOnline = false;
                        await _db.SaveChangesAsync();
                    }
                }
            }
            await base.OnDisconnectedAsync(ex);
        }

        public Task SetChatActive(int contatoId)
        {
            var uid = GetUserId();
            if (uid.HasValue) _activeChatContext[uid.Value] = contatoId;
            return Task.CompletedTask;
        }

        public Task SetGrupoSuporteActive()
        {
            var uid = GetUserId();
            if (uid.HasValue) _activeChatContext[uid.Value] = -1;
            return Task.CompletedTask;
        }

        public Task SetChatInactive()
        {
            var uid = GetUserId();
            if (uid.HasValue) _activeChatContext.TryRemove(uid.Value, out _);
            return Task.CompletedTask;
        }

        // Chamado pelo cliente para enviar uma mensagem
        public async Task SendMessage(int destinatarioId, string conteudo)
        {
            var uid = GetUserId();
            if (uid == null || string.IsNullOrWhiteSpace(conteudo)) return;

            conteudo = conteudo.Trim();
            if (conteudo.Length > 4000) conteudo = conteudo[..4000];

            var msg = new Mensagem
            {
                RemetenteId    = uid.Value,
                DestinatarioId = destinatarioId,
                Conteudo       = conteudo
            };
            _db.Mensagens.Add(msg);
            await _db.SaveChangesAsync();

            var remetente = await _db.Usuarios.FindAsync(uid.Value);
            var destino   = await _db.Usuarios.FindAsync(destinatarioId);

            var payload = new
            {
                id             = msg.Id,
                conteudo       = msg.Conteudo,
                remetenteId    = msg.RemetenteId,
                destinatarioId = msg.DestinatarioId,
                enviadaEm      = msg.EnviadaEm.ToLocalTime().ToString("HH:mm"),
                lida           = false,
                nomeRemetente  = remetente?.NomeCompleto ?? ""
            };

            // Entrega para destinatário e para todas as abas do remetente
            await Clients.Group($"user-{destinatarioId}").SendAsync("ReceiveMessage", payload);
            await Clients.Group($"user-{uid.Value}").SendAsync("ReceiveMessage", payload);

            var chatUrl = destino?.Role switch {
                UserRole.Admin      => $"/Admin/Chat?contatoId={uid.Value}",
                UserRole.Fornecedor => $"/Supplier/Chat?contatoId={uid.Value}",
                _                   => $"/Client/Chat?contatoId={uid.Value}"
            };
            // Não notifica se o destinatário está visualizando esta conversa
            if (!(_activeChatContext.TryGetValue(destinatarioId, out var recipCtx) && recipCtx == uid.Value))
                await _notif.CriarAsync(destinatarioId, "Mensagem recebida.", url: chatUrl);
        }

        // Cliente/Fornecedor envia mensagem ao grupo Suporte
        public async Task SendGrupoMessage(string conteudo)
        {
            var uid = GetUserId();
            if (uid == null || string.IsNullOrWhiteSpace(conteudo)) return;

            conteudo = conteudo.Trim();
            if (conteudo.Length > 4000) conteudo = conteudo[..4000];

            var remetente = await _db.Usuarios.FindAsync(uid.Value);
            if (remetente == null) return;

            var msg = new MensagemGrupo
            {
                Conteudo     = conteudo,
                RemetenteId  = uid.Value,
                ThreadUserId = uid.Value
            };
            _db.MensagensGrupo.Add(msg);
            await _db.SaveChangesAsync();

            var payload = new
            {
                id            = msg.Id,
                conteudo      = msg.Conteudo,
                remetenteId   = msg.RemetenteId,
                threadUserId  = msg.ThreadUserId,
                enviadaEm     = msg.EnviadaEm.ToLocalTime().ToString("HH:mm"),
                nomeRemetente = remetente.NomeCompleto,
                ehSistema     = false
            };

            await Clients.Group("grupo-suporte").SendAsync("ReceiveGrupoMessage", payload);
            await Clients.Group($"user-{uid.Value}").SendAsync("ReceiveGrupoMessage", payload);

            var admins = await _db.Usuarios.Where(u => u.Role == UserRole.Admin).ToListAsync();
            var chatUrl = $"/Admin/Chat?grupoSuporte=true&threadUserId={uid.Value}";
            foreach (var admin in admins)
            {
                // Não notifica admin que está visualizando o grupo suporte
                if (_activeChatContext.TryGetValue(admin.Id, out var adminCtx) && adminCtx == -1)
                    continue;
                await _notif.CriarAsync(admin.Id, "Mensagem de suporte.", url: chatUrl);
            }
        }

        // Admin responde a uma thread do grupo Suporte
        public async Task SendGrupoMessageAdmin(int threadUserId, string conteudo)
        {
            var uid = GetUserId();
            if (uid == null || string.IsNullOrWhiteSpace(conteudo)) return;
            if (Context.User?.IsInRole("Admin") != true) return;

            conteudo = conteudo.Trim();
            if (conteudo.Length > 4000) conteudo = conteudo[..4000];

            var remetente = await _db.Usuarios.FindAsync(uid.Value);
            if (remetente == null) return;

            var msg = new MensagemGrupo
            {
                Conteudo     = conteudo,
                RemetenteId  = uid.Value,
                ThreadUserId = threadUserId
            };
            _db.MensagensGrupo.Add(msg);
            await _db.SaveChangesAsync();

            var payload = new
            {
                id            = msg.Id,
                conteudo      = msg.Conteudo,
                remetenteId   = msg.RemetenteId,
                threadUserId  = msg.ThreadUserId,
                enviadaEm     = msg.EnviadaEm.ToLocalTime().ToString("HH:mm"),
                nomeRemetente = remetente.NomeCompleto,
                ehSistema     = false
            };

            await Clients.Group("grupo-suporte").SendAsync("ReceiveGrupoMessage", payload);
            await Clients.Group($"user-{threadUserId}").SendAsync("ReceiveGrupoMessage", payload);

            var threadUser = await _db.Usuarios.FindAsync(threadUserId);
            var chatUrl = threadUser?.Role == UserRole.Fornecedor
                ? "/Supplier/Chat?grupoSuporte=true"
                : "/Client/Chat?grupoSuporte=true";
            // Não notifica se o usuário está visualizando o grupo suporte
            if (!(_activeChatContext.TryGetValue(threadUserId, out var tCtx) && tCtx == -1))
                await _notif.CriarAsync(threadUserId, "Resposta do suporte.", url: chatUrl);
        }

        // Chamado quando o usuário abre/visualiza uma conversa
        public async Task MarkRead(int remetenteId)
        {
            var uid = GetUserId();
            if (uid == null) return;

            var msgs = await _db.Mensagens
                .Where(m => m.RemetenteId == remetenteId && m.DestinatarioId == uid.Value && !m.Lida)
                .ToListAsync();

            if (!msgs.Any()) return;

            foreach (var m in msgs) m.Lida = true;
            await _db.SaveChangesAsync();

            await Clients.Group($"user-{remetenteId}").SendAsync("MessagesRead", uid.Value);
        }

        public async Task EditarMensagem(int msgId, string novoConteudo)
        {
            var uid = GetUserId(); if (uid == null) return;
            novoConteudo = novoConteudo?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(novoConteudo) || novoConteudo.Length > 4000) return;
            var msg = await _db.Mensagens.FindAsync(msgId);
            if (msg == null || msg.RemetenteId != uid.Value || msg.ApagarParaTodos) return;
            msg.Conteudo = novoConteudo; msg.Editada = true; msg.EditadaEm = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            var p = new { id = msg.Id, conteudo = msg.Conteudo };
            await Clients.Group($"user-{msg.RemetenteId}").SendAsync("MensagemEditada", p);
            await Clients.Group($"user-{msg.DestinatarioId}").SendAsync("MensagemEditada", p);
        }

        public async Task ApagarMensagem(int msgId, bool paraTodos)
        {
            var uid = GetUserId(); if (uid == null) return;
            var msg = await _db.Mensagens.FindAsync(msgId);
            if (msg == null) return;
            if (paraTodos && msg.RemetenteId != uid.Value) return;
            if (paraTodos)
            {
                msg.ApagarParaTodos = true;
                await _db.SaveChangesAsync();
                var p = new { id = msg.Id, paraTodos = true };
                await Clients.Group($"user-{msg.RemetenteId}").SendAsync("MensagemApagada", p);
                await Clients.Group($"user-{msg.DestinatarioId}").SendAsync("MensagemApagada", p);
            }
            else
            {
                var ids = (msg.ApagarParaMim ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                if (!ids.Contains(uid.Value.ToString()))
                { ids.Add(uid.Value.ToString()); msg.ApagarParaMim = string.Join(',', ids); await _db.SaveChangesAsync(); }
                await Clients.Group($"user-{uid.Value}").SendAsync("MensagemApagada", new { id = msg.Id, paraTodos = false });
            }
        }

        public async Task EditarMensagemGrupo(int msgId, string novoConteudo)
        {
            var uid = GetUserId(); if (uid == null) return;
            novoConteudo = novoConteudo?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(novoConteudo) || novoConteudo.Length > 4000) return;
            var msg = await _db.MensagensGrupo.FindAsync(msgId);
            if (msg == null || msg.RemetenteId != uid.Value || msg.ApagarParaTodos) return;
            msg.Conteudo = novoConteudo; msg.Editada = true; msg.EditadaEm = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            var p = new { id = msg.Id, conteudo = msg.Conteudo };
            await Clients.Group("grupo-suporte").SendAsync("MensagemGrupoEditada", p);
            await Clients.Group($"user-{msg.ThreadUserId}").SendAsync("MensagemGrupoEditada", p);
        }

        public async Task ApagarMensagemGrupo(int msgId, bool paraTodos)
        {
            var uid = GetUserId(); if (uid == null) return;
            var msg = await _db.MensagensGrupo.FindAsync(msgId);
            if (msg == null) return;
            if (paraTodos && msg.RemetenteId != uid.Value) return;
            if (paraTodos)
            {
                msg.ApagarParaTodos = true;
                await _db.SaveChangesAsync();
                var p = new { id = msg.Id, paraTodos = true };
                await Clients.Group("grupo-suporte").SendAsync("MensagemGrupoApagada", p);
                await Clients.Group($"user-{msg.ThreadUserId}").SendAsync("MensagemGrupoApagada", p);
            }
            else
            {
                var ids = (msg.ApagarParaMim ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                if (!ids.Contains(uid.Value.ToString()))
                { ids.Add(uid.Value.ToString()); msg.ApagarParaMim = string.Join(',', ids); await _db.SaveChangesAsync(); }
                await Clients.Group($"user-{uid.Value}").SendAsync("MensagemGrupoApagada", new { id = msg.Id, paraTodos = false });
            }
        }
    }
}
