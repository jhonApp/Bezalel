using Amazon.Lambda.Core;
using Bezalel.Workers.AnalisadorBanner.Models;

namespace Bezalel.Workers.AnalisadorBanner.Services;

/// <summary>
/// Persists banner analysis results to the backing store.
/// </summary>
public interface IBannerRepository
{
    Task<string?> GetOriginalImageKeyAsync(string bannerId, ILambdaLogger logger);

    Task SaveAsync(
        string bannerId,
        string originalImageKey,
        int width,
        int height,
        BannerAnalysisResult result,
        ILambdaLogger logger);
}
