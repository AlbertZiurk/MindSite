using System.ComponentModel.DataAnnotations;

namespace MindSite.Entities;

public class Configuracao
{
    public int Id { get; set; }
    [Required, MaxLength(100)] public string Chave { get; set; } = "";
    [Required, MaxLength(500)] public string Valor { get; set; } = "";
}
