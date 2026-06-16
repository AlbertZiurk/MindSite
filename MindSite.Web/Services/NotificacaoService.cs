using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MindSite.Data;
using MindSite.Hubs;
using MindSite.Entities;

namespace MindSite.Services
{
    public class NotificacaoService
    {
        private readonly AppDbContext        _db;
        private readonly IHubContext<ChatHub> _hub;

        public NotificacaoService(AppDbContext db, IHubContext<ChatHub> hub)
        {
            _db  = db;
            _hub = hub;
        }

        public async Task CriarAsync(int usuarioId, string titulo, string? corpo = null, string? url = null, int? servicoId = null)
        {
            Notificacao notif;

            var existing = await _db.Notificacoes.FirstOrDefaultAsync(n =>
                n.UsuarioId == usuarioId &&
                !n.Lida &&
                n.Titulo == titulo &&
                n.Url == url &&
                n.ServicoId == servicoId);

            if (existing != null)
            {
                existing.Contador++;
                existing.CriadaEm = DateTime.UtcNow;
                notif = existing;
            }
            else
            {
                notif = new Notificacao
                {
                    UsuarioId = usuarioId,
                    Titulo    = titulo,
                    Corpo     = corpo,
                    Url       = url,
                    ServicoId = servicoId
                };
                _db.Notificacoes.Add(notif);
            }

            await _db.SaveChangesAsync();

            await _hub.Clients.Group($"user-{usuarioId}").SendAsync("ReceiveNotificacao", new
            {
                id       = notif.Id,
                titulo   = notif.Titulo,
                contador = notif.Contador,
                url      = notif.Url
            });
        }
    }
}
