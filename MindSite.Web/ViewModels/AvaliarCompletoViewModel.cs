using System.ComponentModel.DataAnnotations;

namespace MindSite.ViewModels;

public class AvaliarCompletoViewModel
{
    [Required] public int ServicoId { get; set; }

    [Range(1, 5)] public int EstrelasFornecedor { get; set; }
    [MaxLength(1000)] public string? ComentarioFornecedor { get; set; }

    [Required(ErrorMessage = "Selecione uma avaliação para a plataforma")]
    [Range(1, 5, ErrorMessage = "Avaliação deve ser entre 1 e 5 estrelas")]
    public int EstrelasSistema { get; set; }

    [MaxLength(1000)] public string? ComentarioSistema { get; set; }
}
