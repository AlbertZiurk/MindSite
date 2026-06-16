using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MindSite.Enums;

namespace MindSite.Entities;

public class Proposta
{
    public int Id { get; set; }
    public PropostaStatus Status { get; set; } = PropostaStatus.Pendente;
    [Column(TypeName = "decimal(10,2)")] public decimal Valor { get; set; }
    public DateTime DataEntrega { get; set; }
    [MaxLength(2000)] public string? Mensagem { get; set; }
    public DateTime CriadaEm { get; set; } = DateTime.UtcNow;
    public int ServicoId { get; set; }
    public Servico? Servico { get; set; }
    public bool FechadaPeloAdmin { get; set; } = false;
    public int FornecedorId { get; set; }
    public Usuario? Fornecedor { get; set; }
}
