using System.ComponentModel.DataAnnotations;

namespace MindSite.ViewModels;

public class TrocarSenhaViewModel
{
    [Required] public string SenhaAtual { get; set; } = "";
    [Required, MinLength(6)] public string NovaSenha { get; set; } = "";
    [Required] public string ConfirmarSenha { get; set; } = "";
}
