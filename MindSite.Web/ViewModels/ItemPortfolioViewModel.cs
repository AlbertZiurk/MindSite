using System.ComponentModel.DataAnnotations;

namespace MindSite.ViewModels;

public class ItemPortfolioViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Informe o título")]
    [MaxLength(200)]
    public string Titulo { get; set; } = "";

    [MaxLength(1000)]
    public string? Descricao { get; set; }

    [MaxLength(500)]
    [Url(ErrorMessage = "URL inválida")]
    public string? LinkUrl { get; set; }
}
