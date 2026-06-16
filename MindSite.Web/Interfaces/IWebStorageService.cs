using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace MindSite.Interfaces
{
    public interface IWebStorageService
    {
        Task<string> UploadFormFileAsync(IFormFile file, string pastaDestino);
        Task<string> GerarUrlTemporariaAsync(string blobName, int minutes = 60);
        Task DeletarAsync(string blobName);
    }
}