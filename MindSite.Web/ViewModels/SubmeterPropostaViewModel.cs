using System.ComponentModel.DataAnnotations;

namespace MindSite.ViewModels;

public class SubmeterPropostaViewModel
{
    [Required] public int ServicoId { get; set; }

    [Required(ErrorMessage = "Informe o valor")]
    [Range(0.01, 9999999.99, ErrorMessage = "Valor deve ser maior que zero")]
    public decimal Valor { get; set; }

    public DateTime? DataEntrega { get; set; }

    [MaxLength(2000, ErrorMessage = "Máximo 2000 caracteres")]
    public string? Mensagem { get; set; }
}
