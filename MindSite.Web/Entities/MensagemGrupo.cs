using System.ComponentModel.DataAnnotations;

namespace MindSite.Entities;

public class MensagemGrupo
{
    public int Id { get; set; }
    [Required, MaxLength(8000)] public string Conteudo { get; set; } = "";
    public DateTime EnviadaEm { get; set; } = DateTime.UtcNow;
    public bool EhSistema { get; set; } = false;
    public bool Editada { get; set; } = false;
    public DateTime? EditadaEm { get; set; }
    public bool ApagarParaTodos { get; set; } = false;
    public string? ApagarParaMim { get; set; }
    public int RemetenteId { get; set; }
    public Usuario Remetente { get; set; } = null!;
    public int ThreadUserId { get; set; } // usuário não-admin da conversa
    public Usuario ThreadUser { get; set; } = null!;
}
