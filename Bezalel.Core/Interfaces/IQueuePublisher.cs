using System.Threading.Tasks;

namespace Bezalel.Core.Interfaces
{
    /// <summary>
    /// Core/Application Layer: Interface for queue publishing
    /// </summary>
    public interface IQueuePublisher
    {
        Task PublishAsync<T>(T message, string queueUrl);
    }
}
