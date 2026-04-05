using Bezalel.Workers.StudioWorker.Models;
using SkiaSharp;

namespace Bezalel.Workers.StudioWorker.Services;

/// <summary>
/// Renders individual carousel slides using SkiaSharp.
/// Currently a placeholder — the active pipeline uploads Fal.ai backgrounds directly.
/// This will be fully implemented when text overlay rendering is needed.
/// </summary>
public sealed class SkiaRendererService : ISkiaRendererService
{
    private const string DefaultFontFamily = "Arial";
    private const int    Margin            = 60;

    public byte[] RenderSlide(
        SlideRecord slide,
        PaletteRecord palette,
        byte[] backgroundBytes,
        int canvasWidth  = 1080,
        int canvasHeight = 1080)
    {
        using var surface = SKSurface.Create(new SKImageInfo(canvasWidth, canvasHeight));
        var canvas = surface.Canvas;

        // Fill with palette background color as fallback
        canvas.Clear(ParseHexColor(palette.CorFundoGeral));

        // LAYER 1 — Background (AI-generated image)
        DrawBackground(canvas, backgroundBytes, canvasWidth, canvasHeight);

        // LAYER 2 — Semi-transparent overlay for text legibility (CoverFullImage only)
        if (slide.LayoutType == "CoverFullImage")
        {
            DrawTextOverlay(canvas, canvasWidth, canvasHeight, palette.CorFundoGeral);
        }

        // LAYER 3 — Headline
        var headlineColor = slide.LayoutType == "CoverFullImage"
            ? SKColors.White
            : ParseHexColor(palette.CorTextoPrincipal);

        float yOffset = slide.LayoutType == "CoverFullImage"
            ? canvasHeight * 0.3f
            : Margin + 80;

        yOffset = DrawTextBlock(canvas, slide.Headline, yOffset,
            slide.LayoutType == "CoverFullImage" ? 96 : 64,
            SKFontStyleWeight.ExtraBold, headlineColor,
            SKTextAlign.Center, canvasWidth,
            slide.HighlightWords, ParseHexColor(palette.CorDestaque));

        // LAYER 4 — Body (optional)
        if (!string.IsNullOrWhiteSpace(slide.Body))
        {
            var bodyColor = slide.LayoutType == "CoverFullImage"
                ? new SKColor(224, 224, 224)
                : ParseHexColor(palette.CorTextoPrincipal);

            yOffset += 30;
            DrawTextBlock(canvas, slide.Body, yOffset,
                36, SKFontStyleWeight.Normal, bodyColor,
                SKTextAlign.Center, canvasWidth);
        }

        // Encode as PNG
        using var snapshot = surface.Snapshot();
        using var encoded  = snapshot.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    private static void DrawBackground(SKCanvas canvas, byte[]? bgBytes, int w, int h)
    {
        if (bgBytes is not { Length: > 0 }) return;

        using var data  = SKData.CreateCopy(bgBytes);
        using var image = SKImage.FromEncodedData(data);
        if (image is null) return;

        using var paint = new SKPaint { FilterQuality = SKFilterQuality.High };
        canvas.DrawImage(image, new SKRect(0, 0, w, h), paint);
    }

    private static void DrawTextOverlay(SKCanvas canvas, int w, int h, string bgColorHex)
    {
        var bgColor = ParseHexColor(bgColorHex);
        using var overlayPaint = new SKPaint
        {
            Color = new SKColor(bgColor.Red, bgColor.Green, bgColor.Blue, 140),
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(0, 0, w, h, overlayPaint);
    }

    private float DrawTextBlock(
        SKCanvas canvas, string text, float startY,
        int fontSize, SKFontStyleWeight weight, SKColor color,
        SKTextAlign align, int canvasWidth,
        List<string>? highlightWords = null, SKColor? highlightColor = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return startY;

        using var typeface = SKTypeface.FromFamilyName(
            DefaultFontFamily, weight, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
            ?? SKTypeface.Default;

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color       = color,
            TextSize    = fontSize,
            TextAlign   = align,
            Typeface    = typeface
        };

        float maxWidth = canvasWidth - (Margin * 2);
        var lines = WrapWords(text, paint, maxWidth);
        float lineHeight = paint.FontMetrics.Descent - paint.FontMetrics.Ascent;
        float xPos = canvasWidth / 2f;
        float y = startY;

        foreach (var line in lines)
        {
            // Shadow
            using var shadowPaint = paint.Clone();
            shadowPaint.Color = new SKColor(0, 0, 0, 153);
            canvas.DrawText(line, xPos + 3, y + 3, shadowPaint);

            // Main text
            canvas.DrawText(line, xPos, y, paint);
            y += lineHeight + 8;
        }

        return y;
    }

    private static SKColor ParseHexColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return SKColors.White;
        try { return SKColor.Parse(hex); }
        catch { return SKColors.White; }
    }

    private static List<string> WrapWords(string text, SKPaint paint, float maxWidth)
    {
        var words   = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines   = new List<string>();
        var current = string.Empty;

        foreach (var word in words)
        {
            var candidate = current.Length == 0 ? word : current + " " + word;
            if (paint.MeasureText(candidate) <= maxWidth)
            {
                current = candidate;
            }
            else
            {
                if (current.Length > 0) lines.Add(current);
                current = word;
            }
        }
        if (current.Length > 0) lines.Add(current);
        return lines;
    }
}
