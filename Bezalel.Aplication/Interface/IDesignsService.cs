using Bezalel.Aplication.ViewModel;
using Bezalel.Core.Entities;

namespace Bezalel.Aplication.Interface
{
    public interface IDesignsService
    {
        Task<ResultOperation> CreateAsync(RequestDesign request);
        Task<Design?> GetByIdAsync(string id);
        Task<IEnumerable<Design>> GetAllAsync();
        Task UpdateAsync(Design design);
        Task<bool> DeleteAsync(string id);
    }
}
