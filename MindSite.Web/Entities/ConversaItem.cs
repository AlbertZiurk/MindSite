
using MindSite.Enums;
namespace MindSite.Entities;

public class ConversaItem
{
    public Usuario Contato { get; set; } = new();
    public string UltimaMensagem { get; set; } = "";
    public DateTime? Quando { get; set; }
    public int NaoLidas { get; set; }
    public bool Pinado { get; set; } = false;
    public TipoServico? TipoDoServico { get; set; }
}
