using MindSite.Entities;

namespace MindSite.ViewModels;

public class AdminDashboardViewModel
{
    public int TotalClientes { get; set; }
    public int TotalFornecedores { get; set; }
    public int ServicosPendentes { get; set; }
    public int ServicosAtivos { get; set; }
    public int ServicosConcluidos { get; set; }
    public int FornecedoresPendentes { get; set; }
    public List<LogAcao> AcoesRecentes { get; set; } = new();
    public List<Usuario> SolicitacoesNovosFornecedores { get; set; } = new();
}
