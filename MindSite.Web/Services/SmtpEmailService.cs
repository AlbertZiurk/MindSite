using System.Net;
using System.Net.Mail;
using MindSite.Interfaces;

namespace MindSite.Services
{
    public class SmtpEmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<SmtpEmailService> _logger;

        public SmtpEmailService(IConfiguration config, ILogger<SmtpEmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task EnviarAsync(string para, string assunto, string corpoHtml)
        {
            var host    = _config["Email:SmtpHost"];
            var port    = _config.GetValue<int>("Email:SmtpPort", 587);
            var usuario = _config["Email:Usuario"];
            var senha   = _config["Email:Senha"];
            var de      = _config["Email:De"] ?? "noreply@mindsite.com.br";

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(usuario))
            {
                _logger.LogWarning("E-mail SMTP não configurado — ignorando envio para {Para}", para);
                return;
            }

            using var client = new SmtpClient(host, port)
            {
                Credentials    = new NetworkCredential(usuario, senha),
                EnableSsl      = true,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            using var msg = new MailMessage(de, para, assunto, corpoHtml) { IsBodyHtml = true };
            await client.SendMailAsync(msg);
        }
    }
}
