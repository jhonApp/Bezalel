using Amazon.Lambda.Core;

namespace Bezalel.Workers.ProcessadorArte.Services;

/// <summary>
/// Uploads the final composed image to S3 and returns the public URL.
/// </summary>
public interface IS3StorageService
{
    Task<string> UploadFinalImageAsync(string jobId, byte[] imageBytes, ILambdaLogger logger);
}
