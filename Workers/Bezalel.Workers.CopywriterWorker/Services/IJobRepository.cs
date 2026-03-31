using Amazon.Lambda.Core;

namespace Bezalel.Workers.CopywriterWorker.Services;

/// <summary>
/// Persists carousel job results and status to DynamoDB.
/// </summary>
public interface IJobRepository
{
    Task UpdateStatusAsync(string jobId, string status, ILambdaLogger logger);
    Task SaveCarouselResultAsync(string jobId, string carouselJson, ILambdaLogger logger);
}
