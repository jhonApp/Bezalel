using System.Threading.Tasks;
using Bezalel.Core.Entities;

namespace Bezalel.Core.Interfaces
{
    /// <summary>
    /// Core/Application Layer: Repository interface for DesignEntity
    /// </summary>
    public interface IDesignRepository : IBaseRepository<Design>
    {
        public new Task CreateAsync(Design design);
        Task AddAsync(DesignEntity design);
    }
}
