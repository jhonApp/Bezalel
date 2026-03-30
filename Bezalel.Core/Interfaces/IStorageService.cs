
using System.IO;
using System.Threading.Tasks;

namespace Bezalel.Core.Interfaces
{
    public interface IStorageService
    {
        Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType);
        Task<string> PromoteAssetAsync(string tempKey, string userId);
    }
}
