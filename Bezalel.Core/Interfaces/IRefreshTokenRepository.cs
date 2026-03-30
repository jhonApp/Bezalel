using Bezalel.Core.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bezalel.Core.Interfaces
{
    public interface IRefreshTokenRepository
    {
        Task<RefreshToken?> GetByTokenAsync(string token);
        Task SaveAsync(RefreshToken token);
        Task<IEnumerable<RefreshToken>> GetByUserIdAsync(string userId);
        Task RevokeAllForUserAsync(string userId);
    }
}
