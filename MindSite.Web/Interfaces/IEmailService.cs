namespace MindSite.Interfaces
{
    public interface IEmailService
    {
        Task EnviarAsync(string para, string assunto, string corpoHtml);
    }
}
