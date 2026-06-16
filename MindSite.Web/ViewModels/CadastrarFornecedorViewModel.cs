using System.ComponentModel.DataAnnotations;

namespace MindSite.ViewModels;

public class CadastrarFornecedorViewModel
{
    [Required(ErrorMessage = "Informe o nome")]
    public string NomeCompleto { get; set; } = "";

    [Required(ErrorMessage = "Informe o e-mail")]
    [EmailAddress(ErrorMessage = "E-mail inválido")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Informe o telefone")]
    public string Telefone { get; set; } = "";

    [Required(ErrorMessage = "Informe a senha")]
    [MinLength(6, ErrorMessage = "Mínimo 6 caracteres")]
    public string Senha { get; set; } = "";

    public string? LinkedInUrl { get; set; }
    public string? WhatsAppNumero { get; set; }
    public string? CPF { get; set; }
    public string? SobreVoce { get; set; }
}
