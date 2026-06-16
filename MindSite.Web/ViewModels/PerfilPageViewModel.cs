using MindSite.Entities;

namespace MindSite.ViewModels;

public class PerfilPageViewModel
{
    public Usuario Usuario { get; set; } = new();
    public List<ProjetoPerfilItem> MeusProjetos { get; set; } = new();
    public int TotalServicos { get; set; }
    public int TotalAvaliacoes { get; set; }
    public double MediaEstrelas { get; set; }
}
