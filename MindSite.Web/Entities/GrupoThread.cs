namespace MindSite.Entities;

public class GrupoThread
{
    public Usuario Usuario { get; set; } = null!;
    public string UltimaMensagem { get; set; } = "";
    public DateTime? Quando { get; set; }
}
