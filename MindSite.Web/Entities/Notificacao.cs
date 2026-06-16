using System.ComponentModel.DataAnnotations;

namespace MindSite.Entities;

public class Notificacao
{
    public int Id { get; set; }
    [Required, MaxLength(200)] public string Titulo { get; set; } = "";
    [MaxLength(1000)] public string? Corpo { get; set; }
    public bool Lida { get; set; } = false;
    public int Contador { get; set; } = 1;
    public DateTime CriadaEm { get; set; } = DateTime.UtcNow;
    public string? Url { get; set; }
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public int? ServicoId { get; set; }
}
