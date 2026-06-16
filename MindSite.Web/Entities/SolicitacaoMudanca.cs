using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindSite.Entities;

public class SolicitacaoMudanca
{
    public int Id { get; set; }
    [Required, MaxLength(8000)] public string Descricao { get; set; } = "";
    public bool Aceita { get; set; } = false;
    public bool Recusada { get; set; } = false;
    [MaxLength(2000)] public string? RespostaFornecedor { get; set; }
    [Column(TypeName = "decimal(10,2)")] public decimal? ValorAdicional { get; set; }
    public DateTime CriadaEm { get; set; } = DateTime.UtcNow;
    public int ServicoId { get; set; }
    public Servico? Servico { get; set; }
    public int SolicitanteId { get; set; }
    public Usuario? Solicitante { get; set; }
}
