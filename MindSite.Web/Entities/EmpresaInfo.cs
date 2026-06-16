using System.ComponentModel.DataAnnotations;

namespace MindSite.Entities;

public class EmpresaInfo
{
    public int Id { get; set; }
    [Required, MaxLength(200)] public string NomeEmpresa { get; set; } = "";
    [MaxLength(200)] public string? EmailEmpresa { get; set; }
    [MaxLength(20)] public string? CNPJ { get; set; }
    [MaxLength(20)] public string? TelefoneEmpresa { get; set; }
    [MaxLength(100)] public string? Estado { get; set; }
    [MaxLength(100)] public string? Cidade { get; set; }
    [MaxLength(2000)] public string? SobreVoce { get; set; }
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
}
