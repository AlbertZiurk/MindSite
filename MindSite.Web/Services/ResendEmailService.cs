using MindSite.Interfaces;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers; // Necessário para AuthenticationHeaderValue

namespace MindSite.Services
{
    public class ResendEmailService : IEmailService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly ILogger<ResendEmailService> _logger;

        public ResendEmailService(IHttpClientFactory factory,
                                  IConfiguration config,
                                  ILogger<ResendEmailService> logger)
        {
            _http   = factory.CreateClient("Resend");
            _config = config;
            _logger = logger;
        }

        public async Task EnviarAsync(string para, string assunto, string corpoHtml)
        {
            var apiKey = _config["Resend:ApiKey"];
            var de     = _config["Email:De"] ?? "noreply@mindsite.com.br";

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("Resend API Key não configurada — ignorando envio para {Para}", para);
                return;
            }

            var payload = new
            {
                from    = de,
                to      = new[] { para },
                subject = assunto,
                html    = corpoHtml
            };

            var json    = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // CORREÇÃO AQUI: Criamos a mensagem explicitamente para isolar o Header por envio
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
            {
                Content = content
            };
            
            // Adiciona a autorização de forma segura e thread-safe
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await _http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("Resend retornou {Status}: {Body}", response.StatusCode, body);
                throw new InvalidOperationException($"Falha ao enviar e-mail via Resend: {response.StatusCode}");
            }

            _logger.LogInformation("E-mail enviado via Resend para {Para} — assunto: {Assunto}", para, assunto);
        }
    }
}