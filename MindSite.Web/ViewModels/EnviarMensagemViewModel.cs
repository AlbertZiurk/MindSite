using System.ComponentModel.DataAnnotations;

namespace MindSite.ViewModels;

public class EnviarMensagemViewModel
{
    [Required] public int DestinatarioId { get; set; }
    [Required] public string Conteudo { get; set; } = "";
}
