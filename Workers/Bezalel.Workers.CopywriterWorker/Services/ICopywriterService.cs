using Amazon.Lambda.Core;

namespace Bezalel.Workers.CopywriterWorker.Services;

/// <summary>
/// Calls Claude to generate a structured CarouselJob JSON
/// based on the user's theme/prompt.
/// </summary>
public interface ICopywriterService
{
    /// <summary>
    /// Sends the user's theme to Claude and returns the raw JSON string
    /// containing the full carousel structure (slides, palette, layout).
    /// </summary>
    Task<string> GenerateCarouselAsync(
        string tema,
        int slideCount,
        string? visualStyle,
        ILambdaLogger logger,
        string jobId);
}
