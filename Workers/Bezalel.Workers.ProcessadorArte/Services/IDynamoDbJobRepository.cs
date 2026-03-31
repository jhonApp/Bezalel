using Amazon.Lambda.Core;
using Bezalel.Workers.ProcessadorArte.Models;

namespace Bezalel.Workers.ProcessadorArte.Services;

/// <summary>
/// Reads carousel job data from DynamoDB and updates job status records.
/// </summary>
public interface IDynamoDbJobRepository
{
    Task<CarouselJobRecord> GetCarouselJobAsync(string carouselJobId, ILambdaLogger logger);
    Task UpdateJobStatusAsync(string jobId, string status, ILambdaLogger logger, string? finalUrl = null);
    Task UpdateJobWithSlideUrlsAsync(string jobId, List<string> slideUrls, ILambdaLogger logger);
}
