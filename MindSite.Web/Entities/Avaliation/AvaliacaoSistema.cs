using System.ComponentModel.DataAnnotations;

namespace MindSite.Entities;

public class AvaliacaoSistema
{
    public int Id { get; set; }
    [Range(1, 5)] public int Estrelas { get; set; }
    [MaxLength(1000)] public string? Comentario { get; set; }
    public DateTime CriadaEm { get; set; } = DateTime.UtcNow;
    public int ServicoId { get; set; }
    public Servico? Servico { get; set; }
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
}
