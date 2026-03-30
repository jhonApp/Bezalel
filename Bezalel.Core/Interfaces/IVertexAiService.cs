using System.Threading.Tasks;

namespace Bezalel.Core.Interfaces
{
    public interface IVertexAiService
    {
        Task<string> GenerateImageAsync(string prompt);
        Task<string> GenerateTextAsync(string prompt);
    }
}
