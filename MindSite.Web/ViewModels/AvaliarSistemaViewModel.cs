using System.ComponentModel.DataAnnotations;

namespace MindSite.ViewModels;

public class AvaliarSistemaViewModel
{
    [Required] public int ServicoId { get; set; }

    [Required(ErrorMessage = "Selecione uma avaliação")]
    [Range(1, 5, ErrorMessage = "Avaliação deve ser entre 1 e 5 estrelas")]
    public int Estrelas { get; set; }

    [MaxLength(1000, ErrorMessage = "Máximo 1000 caracteres")]
    public string? Comentario { get; set; }
}
