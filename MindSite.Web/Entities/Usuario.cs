using System.ComponentModel.DataAnnotations;
using MindSite.Enums;
namespace MindSite.Entities;

public class Usuario
{
    public int Id { get; set; }
    [Required, MaxLength(150)] public string NomeCompleto { get; set; } = "";
    [Required, MaxLength(200)] public string Email { get; set; } = "";
    [Required, MaxLength(20)] public string Telefone { get; set; } = "";
    [Required] public string SenhaHash { get; set; } = "";
    public UserRole Role { get; set; } = UserRole.Cliente;
    public UserStatus Status { get; set; } = UserStatus.Ativo;
    public bool IsOnline { get; set; } = false;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime? UltimoAcesso { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? WhatsAppNumero { get; set; }
    [MaxLength(14)] public string? CPF { get; set; }
    [MaxLength(2000)] public string? SobreVoce { get; set; }
    public DateTime? AceitouContratoClienteEm { get; set; }
    public DateTime? AceitouContratoFornecedorEm { get; set; }
    public string? TokenResetSenha { get; set; }
    public DateTime? TokenResetExpira { get; set; }
    public string? FotoPerfilUrl { get; set; }
    public string? Endereco { get; set; }
    public bool NotificacoesEmail { get; set; } = true;
    public bool NotificacoesChat { get; set; } = true;
    public EmpresaInfo? Empresa { get; set; }
    public ICollection<Servico> ServicosCliente { get; set; } = new List<Servico>();
    public ICollection<Servico> ServicosFornecedor { get; set; } = new List<Servico>();
    public ICollection<Proposta> PropostasFeitas { get; set; } = new List<Proposta>();
    public ICollection<Mensagem> MensagensEnviadas { get; set; } = new List<Mensagem>();
    public ICollection<Mensagem> MensagensRecebidas { get; set; } = new List<Mensagem>();
    public ICollection<Notificacao> Notificacoes { get; set; } = new List<Notificacao>();
    public ICollection<LogAcao> Logs { get; set; } = new List<LogAcao>();
    public ICollection<Arquivo> Arquivos { get; set; } = new List<Arquivo>();
    public ICollection<Avaliacao> AvaliacoesCliente { get; set; } = new List<Avaliacao>();
    public ICollection<Avaliacao> AvaliacoesFornecedor { get; set; } = new List<Avaliacao>();
    public ICollection<AvaliacaoSistema> AvaliacoesSistema { get; set; } = new List<AvaliacaoSistema>();
    public ICollection<ItemPortfolio> Portfolio { get; set; } = new List<ItemPortfolio>();
}
