using Amazon.Lambda.Core;

namespace Bezalel.Workers.StudioWorker.Services;

public interface IFalApiService
{
    Task<byte[]> GenerateImageAsync(
        string masterPrompt, 
        ILambdaLogger logger,
        string jobId,
        string aspectRatio);

    Task<byte[]> RemoveBackgroundAsync(
        byte[] imageBytes,
        ILambdaLogger logger,
        string jobId);
}
