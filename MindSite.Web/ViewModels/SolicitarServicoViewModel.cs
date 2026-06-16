using System.ComponentModel.DataAnnotations;

using  MindSite.Enums;
namespace MindSite.ViewModels;

public class SolicitarServicoViewModel
{
    [Required] public TipoServico Tipo { get; set; }
    [Required] public string Requisitos { get; set; } = "";
    public string? NomeProjeto { get; set; }
    public int? FornecedorId { get; set; }
    [Required] public PrazoTipo? PrazoCliente { get; set; }
    public DateTime? DataPrazoCliente { get; set; }
    public decimal? OrcamentoCliente { get; set; }
}
