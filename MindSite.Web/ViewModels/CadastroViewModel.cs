using System.ComponentModel.DataAnnotations;

namespace MindSite.ViewModels;

public class CadastroViewModel
{
    [Required(ErrorMessage = "Informe o nome")]
    [MaxLength(150, ErrorMessage = "Máximo 150 caracteres")]
    public string NomeCompleto { get; set; } = "";

    [Required(ErrorMessage = "Informe o e-mail")]
    [EmailAddress(ErrorMessage = "E-mail inválido")]
    [MaxLength(200)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Informe o telefone")]
    [MaxLength(20)]
    [RegularExpression(@"^\(\d{2}\) \d{4,5}-\d{4}$", ErrorMessage = "Telefone inválido — use o formato (11) 99999-9999")]
    public string Telefone { get; set; } = "";

    [Required(ErrorMessage = "Informe a senha")]
    [MinLength(6, ErrorMessage = "Mínimo 6 caracteres")]
    [MaxLength(100)]
    public string Senha { get; set; } = "";

    [Required(ErrorMessage = "Confirme a senha")]
    [Compare("Senha", ErrorMessage = "Senhas não conferem")]
    public string ConfirmarSenha { get; set; } = "";

    [Range(typeof(bool), "true", "true", ErrorMessage = "Você deve aceitar os termos de uso")]
    public bool AceitouContrato { get; set; }
}
