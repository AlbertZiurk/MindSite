using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MindSite.Interfaces;
using System;
using System.IO;
using System.Threading.Tasks;

namespace MindSite.Services
{
    public class LocalArquivoStorageService : IArquivoStorageService
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<LocalArquivoStorageService> _logger;

        public LocalArquivoStorageService(IWebHostEnvironment env, ILogger<LocalArquivoStorageService> logger)
        {
            _env = env;
            _logger = logger;
        }

        // 1. Implementação para Stream (Usada no Chat)
        public async Task<string> UploadAsync(Stream arquivoStream, string nomeArquivo, string pasta)
        {
            var pathDiretorio = Path.Combine(_env.WebRootPath, "uploads", pasta);
            Directory.CreateDirectory(pathDiretorio);

            var nomeFinal = $"{Guid.NewGuid()}_{nomeArquivo}";
            var pathCompleto = Path.Combine(pathDiretorio, nomeFinal);

            using (var fileStream = new FileStream(pathCompleto, FileMode.Create))
            {
                await arquivoStream.CopyToAsync(fileStream);
            }

            return $"/uploads/{pasta}/{nomeFinal}".Replace('\\', '/');
        }

        // 2. Implementação para FormFile (Usada nos Formulários)
        public async Task<string> UploadFormFileAsync(IFormFile file, string pastaDestino)
        {
            using var stream = file.OpenReadStream();
            return await UploadAsync(stream, file.FileName, pastaDestino);
        }

        public Task<string> GerarUrlTemporariaAsync(string blobName, int minutes = 60)
        {
            var url = blobName.StartsWith("/") ? blobName : $"/uploads/{blobName}";
            return Task.FromResult(url.Replace('\\', '/'));
        }

        public Task DeletarAsync(string blobName)
        {
            var limpo = blobName.Replace("/uploads/", "").Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(_env.WebRootPath, "uploads", limpo);
            
            if (System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);
                
            return Task.CompletedTask;
        }
    }
}