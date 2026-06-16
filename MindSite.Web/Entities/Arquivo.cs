using System.ComponentModel.DataAnnotations;

namespace MindSite.Entities;

public class Arquivo
{
    public int Id { get; set; }
    [Required, MaxLength(500)] public string NomeOriginal { get; set; } = "";
    [Required, MaxLength(500)] public string Caminho { get; set; } = "";
    [MaxLength(50)] public string? MimeType { get; set; }
    public long Tamanho { get; set; }
    public DateTime UploadEm { get; set; } = DateTime.UtcNow;
    public int ServicoId { get; set; }
    public Servico? Servico { get; set; }
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
}
