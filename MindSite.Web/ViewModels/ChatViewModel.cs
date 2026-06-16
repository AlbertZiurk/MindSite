using MindSite.Entities;

namespace MindSite.ViewModels;

public class ChatViewModel
{
    public Usuario UsuarioAtual { get; set; } = new();
    public List<ConversaItem> Conversas { get; set; } = new();
    public int? ContatoSelecionadoId { get; set; }
    public Usuario? ContatoSelecionado { get; set; }
    public List<Mensagem> Mensagens { get; set; } = new();
    public List<Usuario> ContatosPinados { get; set; } = new();
    // Grupo Suporte
    public bool IsGrupoSuporte { get; set; }
    public List<MensagemGrupo> GrupoMensagens { get; set; } = new();
    public string GrupoUltimaMensagem { get; set; } = "";
    public DateTime? GrupoUltimaData { get; set; }
    // Admin only: navegação de threads
    public List<GrupoThread> GrupoThreads { get; set; } = new();
    public int? GrupoThreadUserId { get; set; }
    public Usuario? GrupoThreadUser { get; set; }
    // true quando não há serviço ativo entre o usuário atual e o contato selecionado
    public bool ChatBloqueado { get; set; }
}
