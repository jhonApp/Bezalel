using Amazon.Lambda.Core;
using Bezalel.Workers.AnalisadorBanner.Models;

namespace Bezalel.Workers.AnalisadorBanner.Services;

/// <summary>
/// Sends a Base64-encoded image to a Vision API and returns structured design metadata.
/// </summary>
public interface IVisionApiService
{
    Task<BannerAnalysisResult> AnalyzeImageAsync(
        string base64Image,
        ILambdaLogger logger,
        string requestId);
}
