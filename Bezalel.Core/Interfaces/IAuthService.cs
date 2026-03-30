using Bezalel.Core.Entities;
using System.Threading.Tasks;

namespace Bezalel.Core.Interfaces
{
    public interface IAuthService
    {
        Task<User> RegisterAsync(string email, string password);
        Task<(string AccessToken, string RefreshToken)> LoginAsync(string email, string password);
        Task<(string AccessToken, string RefreshToken)> RefreshTokenAsync(string token, string refreshToken);
    }
}
