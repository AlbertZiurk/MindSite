using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MindSite.Interfaces;
using System;
using System.IO;
using System.Threading.Tasks;

namespace MindSite.Services
{
    public class ArquivoStorageService : IArquivoStorageService
    {
        // Uma forma ainda mais limpa e desacoplada de receber o cliente do Azure
        private readonly BlobServiceClient _client;
        private readonly string _container;
        private readonly ILogger<ArquivoStorageService> _logger;

        public ArquivoStorageService(BlobServiceClient client, IConfiguration config, ILogger<ArquivoStorageService> logger)
        {
            _client = client;
            _logger = logger;
            _container = config["Azure:BlobStorage:Container"] ?? "mindsite-uploads";
        }

        private async Task<BlobContainerClient> GetContainerAsync()
        {
            var container = _client.GetBlobContainerClient(_container);
            await container.CreateIfNotExistsAsync(PublicAccessType.None);
            return container;
        }

        // 1. Novo Método de Stream adaptado para o Azure Blob
        public async Task<string> UploadAsync(Stream arquivoStream, string nomeArquivo, string pasta)
        {
            var container = await GetContainerAsync();
            var ext = Path.GetExtension(nomeArquivo);
            var blobName = $"{pasta.Trim('/')}/{Guid.NewGuid()}{ext}";
            var blob = container.GetBlobClient(blobName);

            await blob.UploadAsync(arquivoStream, new BlobUploadOptions());

            _logger.LogInformation("Arquivo via Stream enviado para Azure Blob: {BlobName}", blobName);
            return blobName;
        }

        // 2. Método de FormFile reaproveitando a lógica acima
        public async Task<string> UploadFormFileAsync(IFormFile file, string pastaDestino)
        {
            var container = await GetContainerAsync();
            var ext = Path.GetExtension(file.FileName);
            var blobName = $"{pastaDestino.Trim('/')}/{Guid.NewGuid()}{ext}";
            var blob = container.GetBlobClient(blobName);

            var headers = new BlobHttpHeaders { ContentType = file.ContentType };

            await using var stream = file.OpenReadStream();
            await blob.UploadAsync(stream, new BlobUploadOptions { HttpHeaders = headers });

            _logger.LogInformation("FormFile enviado para Azure Blob: {BlobName}", blobName);
            return blobName;
        }

        public async Task<string> GerarUrlTemporariaAsync(string blobName, int minutes = 60)
        {
            var container = await GetContainerAsync();
            var blob = container.GetBlobClient(blobName);

            if (!blob.CanGenerateSasUri)
                throw new InvalidOperationException("O BlobClient não pode gerar SAS URI.");

            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = _container,
                BlobName = blobName,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(minutes)
            };
            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            return blob.GenerateSasUri(sasBuilder).ToString();
        }

        public async Task DeletarAsync(string blobName)
        {
            var container = await GetContainerAsync();
            var blob = container.GetBlobClient(blobName);
            await blob.DeleteIfExistsAsync();
        }
    }
}