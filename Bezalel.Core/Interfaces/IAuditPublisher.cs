using System.Threading.Tasks;
using Bezalel.Core.Entities;

namespace Bezalel.Core.Interfaces
{
    public interface IAuditPublisher
    {
        Task PublishAsync(AuditLogEntry entry);
    }
}
