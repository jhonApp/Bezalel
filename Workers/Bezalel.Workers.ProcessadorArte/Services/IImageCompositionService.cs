using Amazon.Lambda.Core;
using Bezalel.Workers.ProcessadorArte.Models;

namespace Bezalel.Workers.ProcessadorArte.Services;

/// <summary>
/// Applies final typography overlays.
/// </summary>
public interface IImageCompositionService
{
    Task<byte[]> ApplyTypographyAsync(
        byte[] aiGeneratedImageBytes, BannerAnalysisResult bannerMetadata, string userText, ILambdaLogger logger);
}
