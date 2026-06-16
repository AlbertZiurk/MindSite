using System.ComponentModel.DataAnnotations;

namespace MindSite.ViewModels;

public class EditarDadosViewModel
{
    [Required] public string NomeCompleto { get; set; } = "";
    [Required] public string Telefone { get; set; } = "";
    public string? Endereco { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? WhatsAppNumero { get; set; }
    public string? CPF { get; set; }
    public string? SobreVoce { get; set; }
}
