using System.ComponentModel.DataAnnotations;

namespace MindSite.ViewModels;

public class PerfilViewModel
{
    [Required] public string NomeCompleto { get; set; } = "";
    [Required] public string Telefone { get; set; } = "";
    public string? SenhaAtual { get; set; }
    public string? NovaSenha { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? WhatsAppNumero { get; set; }
    public string? CPF { get; set; }
    public string? SobreVoce { get; set; }
}
