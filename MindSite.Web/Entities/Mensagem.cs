using System.ComponentModel.DataAnnotations;

namespace MindSite.Entities;

public class Mensagem
{
    public int Id { get; set; }
    [Required, MaxLength(8000)] public string Conteudo { get; set; } = "";
    public bool Lida { get; set; } = false;
    public bool Editada { get; set; } = false;
    public DateTime? EditadaEm { get; set; }
    public bool ApagarParaTodos { get; set; } = false;
    public string? ApagarParaMim { get; set; }
    public DateTime EnviadaEm { get; set; } = DateTime.UtcNow;
    public int RemetenteId { get; set; }
    public Usuario? Remetente { get; set; }
    public int DestinatarioId { get; set; }
    public Usuario? Destinatario { get; set; }
}
