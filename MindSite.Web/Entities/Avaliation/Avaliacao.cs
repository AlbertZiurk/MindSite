using System.ComponentModel.DataAnnotations;

namespace MindSite.Entities;

public class Avaliacao
{
    public int Id { get; set; }
    [Range(1, 5)] public int Estrelas { get; set; }
    [MaxLength(1000)] public string? Comentario { get; set; }
    public DateTime CriadaEm { get; set; } = DateTime.UtcNow;
    public int ServicoId { get; set; }
    public Servico? Servico { get; set; }
    public int ClienteId { get; set; }
    public Usuario? Cliente { get; set; }
    public int FornecedorId { get; set; }
    public Usuario? Fornecedor { get; set; }
}
