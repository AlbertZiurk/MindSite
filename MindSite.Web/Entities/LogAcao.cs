using System.ComponentModel.DataAnnotations;

namespace MindSite.Entities;

public class LogAcao
{
    public int Id { get; set; }
    [Required, MaxLength(500)] public string Descricao { get; set; } = "";
    public DateTime RealizadaEm { get; set; } = DateTime.UtcNow;
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
}
