using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;

namespace MindSite.Interfaces
{
    public interface IArquivoStorageService
    {
        // Aceita streams puras (usado no chat unificado)
        Task<string> UploadAsync(Stream arquivoStream, string nomeArquivo, string pasta);
        
        // Aceita o arquivo direto do formulário HTTP (usado nas controllers tradicionais)
        Task<string> UploadFormFileAsync(IFormFile file, string pastaDestino);
        
        Task<string> GerarUrlTemporariaAsync(string blobName, int minutes = 60);
        Task DeletarAsync(string blobName);
    }
}