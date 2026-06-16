namespace MindSite.Entities;

public class ProjetoPerfilItem
{
    public string NomeProjeto { get; set; } = "";
    public string NomeFornecedor { get; set; } = "";
    public DateTime? Inicio { get; set; }
    public string Status { get; set; } = "";
    public string StatusClass { get; set; } = "";
}
