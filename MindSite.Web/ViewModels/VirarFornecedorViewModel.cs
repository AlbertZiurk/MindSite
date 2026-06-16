using System.ComponentModel.DataAnnotations;

namespace MindSite.ViewModels;

public class VirarFornecedorViewModel
{
    [Required(ErrorMessage = "Informe o LinkedIn")]
    [Url(ErrorMessage = "URL inválida")]
    public string LinkedInUrl { get; set; } = "";

    [Required(ErrorMessage = "Informe o WhatsApp")]
    [RegularExpression(@"[\d\s\(\)\+\-]{8,20}", ErrorMessage = "Número inválido")]
    public string WhatsAppNumero { get; set; } = "";

    [MaxLength(14)]
    [RegularExpression(@"^\d{3}\.\d{3}\.\d{3}-\d{2}$", ErrorMessage = "CPF inválido (000.000.000-00)")]
    public string? CPF { get; set; }

    [MaxLength(2000)]
    public string? SobreVoce { get; set; }
}
