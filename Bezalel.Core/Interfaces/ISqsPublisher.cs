using Bezalel.Core.DTOs;

namespace Bezalel.Core.Interfaces
{
    /// <summary>
    /// Publishes messages to the SQS Analysis Queue.
    /// Separated from IQueuePublisher to follow Interface Segregation — the analysis
    /// queue URL is resolved internally, so callers don't need to know it.
    /// </summary>
    public interface ISqsPublisher
    {
        Task PublishToAnalysisQueueAsync(AnalysisJobMessage message);
    }
}
