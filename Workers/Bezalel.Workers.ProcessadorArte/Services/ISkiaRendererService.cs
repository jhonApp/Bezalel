using Bezalel.Workers.ProcessadorArte.Models;

namespace Bezalel.Workers.ProcessadorArte.Services;

/// <summary>
/// Renders individual carousel slides using SkiaSharp.
/// Each slide is composed of an AI-generated background + text overlays.
/// </summary>
public interface ISkiaRendererService
{
    /// <summary>
    /// Renders a single carousel slide: background image + headline + body + CTA.
    /// Returns the encoded PNG bytes.
    /// </summary>
    byte[] RenderSlide(
        SlideRecord slide,
        PaletteRecord palette,
        byte[] backgroundBytes,
        int canvasWidth  = 1080,
        int canvasHeight = 1080);
}
