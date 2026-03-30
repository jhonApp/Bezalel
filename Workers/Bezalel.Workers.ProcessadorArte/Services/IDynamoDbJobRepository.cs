using Amazon.Lambda.Core;
using Bezalel.Workers.ProcessadorArte.Models;

namespace Bezalel.Workers.ProcessadorArte.Services;

/// <summary>
/// Reads banner metadata from DynamoDB and updates job status records.
/// </summary>
public interface IDynamoDbJobRepository
{
    Task<BannerAnalysisResult> GetBannerMetadataAsync(string bannerId, ILambdaLogger logger);
    Task<BannerFullRecord> GetBannerFullRecordAsync(string bannerId, ILambdaLogger logger);
    Task UpdateJobStatusAsync(string jobId, string status, ILambdaLogger logger, string? finalUrl = null);
}
