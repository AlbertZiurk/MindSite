using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindSite.Entities
{
    public class ItemPortfolio
    {
        public int Id { get; set; }
        [Required, MaxLength(200)] public string Titulo     { get; set; } = "";
        [MaxLength(1000)]          public string? Descricao { get; set; }
        [MaxLength(500)]           public string? LinkUrl   { get; set; }
        [MaxLength(500)]           public string? ImagemCaminho { get; set; }
        public DateTime CriadoEm  { get; set; } = DateTime.UtcNow;
        public int  FornecedorId  { get; set; }
        public Usuario? Fornecedor { get; set; }
    }
}
