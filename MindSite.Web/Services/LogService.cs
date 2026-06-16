using MindSite.Data;
using MindSite.Entities;

namespace MindSite.Services
{
    public class LogService
    {
        private readonly AppDbContext _db;
        public LogService(AppDbContext db) => _db = db;

        public async Task RegistrarAsync(int usuarioId, string descricao)
        {
            _db.Logs.Add(new LogAcao { UsuarioId = usuarioId, Descricao = descricao });
            await _db.SaveChangesAsync();
        }
    }
}
