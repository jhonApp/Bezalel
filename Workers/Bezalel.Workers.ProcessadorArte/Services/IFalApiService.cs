using Amazon.Lambda.Core;

namespace Bezalel.Workers.ProcessadorArte.Services;

public interface IFalApiService
{
    Task<byte[]> GenerateImageAsync(
        string masterPrompt, 
        ILambdaLogger logger,
        string jobId);

    Task<byte[]> RemoveBackgroundAsync(
        byte[] imageBytes,
        ILambdaLogger logger,
        string jobId);
}
