using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MindSite.Enums;

namespace MindSite.Entities;

public class Servico
{
    public int Id { get; set; }
    public TipoServico Tipo { get; set; }
    public ServicoStatus Status { get; set; } = ServicoStatus.Pendente;
    [MaxLength(200)] public string? NomeProjeto { get; set; }
    [MaxLength(8000)] public string? Requisitos { get; set; }
    [MaxLength(2000)] public string? Observacoes { get; set; }
    [Column(TypeName = "decimal(10,2)")] public decimal? Valor { get; set; }
    [Column(TypeName = "decimal(10,2)")] public decimal? OrcamentoCliente { get; set; }
    [Column(TypeName = "decimal(10,2)")] public decimal? TaxaPlataforma { get; set; }
    [Column(TypeName = "decimal(10,2)")] public decimal ValorAdicional { get; set; }
    public bool PagamentoInicialFeito { get; set; } = false;
    public bool PagamentoFinalFeito { get; set; } = false;
    public string? StripeSessionIdInicial { get; set; }
    public string? StripePaymentIntentIdInicial { get; set; }
    public DateTime? PagamentoInicialEm { get; set; }
    public string? StripeSessionIdFinal { get; set; }
    public string? StripePaymentIntentIdFinal { get; set; }
    public DateTime? PagamentoFinalEm { get; set; }
    public DateTime? DataEntrega { get; set; }
    public PrazoTipo? PrazoCliente { get; set; }
    public DateTime? DataPrazoCliente { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime? AtualizadoEm { get; set; }
    public int ClienteId { get; set; }
    public Usuario? Cliente { get; set; }
    public int? FornecedorId { get; set; }
    public Usuario? Fornecedor { get; set; }
    public ICollection<SolicitacaoMudanca> Mudancas { get; set; } = new List<SolicitacaoMudanca>();
    public ICollection<Proposta> Propostas { get; set; } = new List<Proposta>();
    public ICollection<Arquivo> Arquivos { get; set; } = new List<Arquivo>();
    public Avaliacao? Avaliacao { get; set; }
    public ICollection<AvaliacaoSistema> AvaliacoesSistema { get; set; } = new List<AvaliacaoSistema>();

    // Valor líquido calculado que vai para o bolso do Fornecedor
    public decimal? ValorRepasseFornecedor { get; set; } 
    
    // Identifica se o dinheiro já foi transferido para o saldo do fornecedor
    public bool RepasseEfetuado { get; set; }

    
}
