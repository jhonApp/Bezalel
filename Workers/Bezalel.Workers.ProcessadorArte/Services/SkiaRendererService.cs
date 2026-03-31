using Bezalel.Workers.ProcessadorArte.Models;
using SkiaSharp;

namespace Bezalel.Workers.ProcessadorArte.Services;

/// <summary>
/// Renders individual carousel slides using SkiaSharp.
/// Composition layers (bottom to top):
///   1 — AI-generated background (clean, no text)
///   2 — Headline text
///   3 — Body text (optional)
///   4 — Call to Action pill (optional, last slide)
/// </summary>
public sealed class SkiaRendererService : ISkiaRendererService
{
    private const string DefaultFontFamily = "Arial";
    private const int    ShadowOffset      = 3;
    private const byte   ShadowAlpha       = 153; // ~60% opacity
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
        canvas.Clear(ParseHexColor(palette.BackgroundColor));

        // ═══════════════════════════════════════════════════════════════════
        //  LAYER 1 — BACKGROUND (AI-generated clean image)
        // ═══════════════════════════════════════════════════════════════════
        DrawBackground(canvas, backgroundBytes, canvasWidth, canvasHeight);

        // ═══════════════════════════════════════════════════════════════════
        //  LAYER 2 — Semi-transparent overlay for text legibility
        // ═══════════════════════════════════════════════════════════════════
        DrawTextOverlay(canvas, canvasWidth, canvasHeight, palette.BackgroundColor);

        // ═══════════════════════════════════════════════════════════════════
        //  LAYER 3 — HEADLINE TEXT
        // ═══════════════════════════════════════════════════════════════════
        var layout = slide.Layout;
        var textAlign = ResolveTextAlign(layout.TextAlignment);
        var headlineWeight = ResolveFontWeight(layout.HeadlineFontWeight);
        var headlineColor = ParseHexColor(layout.HeadlineColor);
        var bodyColor = ParseHexColor(layout.BodyColor);

        float yOffset = ResolveYStart(layout.TextPosition, canvasHeight);

        // Draw headline
        yOffset = DrawTextBlock(
            canvas, slide.Headline, yOffset,
            layout.HeadlineFontSize, headlineWeight, headlineColor,
            textAlign, canvasWidth);

        // ═══════════════════════════════════════════════════════════════════
        //  LAYER 4 — BODY TEXT (optional)
        // ═══════════════════════════════════════════════════════════════════
        if (!string.IsNullOrWhiteSpace(slide.Body))
        {
            yOffset += 30; // spacing between headline and body
            yOffset = DrawTextBlock(
                canvas, slide.Body, yOffset,
                layout.BodyFontSize, SKFontStyleWeight.Normal, bodyColor,
                textAlign, canvasWidth);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  LAYER 5 — CTA PILL (optional, typically on last slide)
        // ═══════════════════════════════════════════════════════════════════
        if (!string.IsNullOrWhiteSpace(slide.CallToAction))
        {
            var accentColor = ParseHexColor(palette.AccentColor);
            DrawCtaPill(canvas, slide.CallToAction, accentColor, canvasWidth, canvasHeight);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  ENCODE — PNG output
        // ═══════════════════════════════════════════════════════════════════
        using var snapshot = surface.Snapshot();
        using var encoded  = snapshot.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    // ───────────────────────────────────────────────────────────────────────
    //  LAYER 1 — Background
    // ───────────────────────────────────────────────────────────────────────

    private static void DrawBackground(SKCanvas canvas, byte[]? bgBytes, int w, int h)
    {
        if (bgBytes is not { Length: > 0 }) return;

        using var data  = SKData.CreateCopy(bgBytes);
        using var image = SKImage.FromEncodedData(data);
        if (image is null) return;

        using var paint = new SKPaint { FilterQuality = SKFilterQuality.High };
        canvas.DrawImage(image, new SKRect(0, 0, w, h), paint);
    }

    // ───────────────────────────────────────────────────────────────────────
    //  LAYER 2 — Overlay for legibility
    // ───────────────────────────────────────────────────────────────────────

    private static void DrawTextOverlay(SKCanvas canvas, int w, int h, string bgColorHex)
    {
        var bgColor = ParseHexColor(bgColorHex);
        using var overlayPaint = new SKPaint
        {
            Color = new SKColor(bgColor.Red, bgColor.Green, bgColor.Blue, 140), // ~55% opacity
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(0, 0, w, h, overlayPaint);
    }

    // ───────────────────────────────────────────────────────────────────────
    //  Text Block Drawing
    // ───────────────────────────────────────────────────────────────────────

    private float DrawTextBlock(
        SKCanvas canvas, string text, float startY,
        int fontSize, SKFontStyleWeight weight, SKColor color,
        SKTextAlign align, int canvasWidth)
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

        using var shadowPaint = new SKPaint
        {
            IsAntialias = true,
            Color       = new SKColor(0, 0, 0, ShadowAlpha),
            TextSize    = fontSize,
            TextAlign   = align,
            Typeface    = typeface
        };

        float maxWidth = canvasWidth - (Margin * 2);
        var lines = WrapWords(text, paint, maxWidth);
        float lineHeight = paint.FontMetrics.Descent - paint.FontMetrics.Ascent;
        float xPos = ResolveTextX(align, canvasWidth);
        float y = startY;

        foreach (var line in lines)
        {
            canvas.DrawText(line, xPos + ShadowOffset, y + ShadowOffset, shadowPaint);
            canvas.DrawText(line, xPos, y, paint);
            y += lineHeight + 8; // 8px line spacing
        }

        return y;
    }

    // ───────────────────────────────────────────────────────────────────────
    //  CTA Pill
    // ───────────────────────────────────────────────────────────────────────

    private static void DrawCtaPill(SKCanvas canvas, string ctaText, SKColor accentColor, int canvasWidth, int canvasHeight)
    {
        using var typeface = SKTypeface.FromFamilyName(
            DefaultFontFamily, SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
            ?? SKTypeface.Default;

        using var textPaint = new SKPaint
        {
            IsAntialias = true,
            Color       = SKColors.White,
            TextSize    = 40,
            TextAlign   = SKTextAlign.Center,
            Typeface    = typeface
        };

        float textWidth = textPaint.MeasureText(ctaText);
        float pillWidth = textWidth + 80;
        float pillHeight = 70;
        float pillX = (canvasWidth - pillWidth) / 2f;
        float pillY = canvasHeight - 140;

        // Draw rounded rect background
        using var pillPaint = new SKPaint
        {
            IsAntialias = true,
            Color       = accentColor,
            Style       = SKPaintStyle.Fill
        };

        var pillRect = new SKRoundRect(new SKRect(pillX, pillY, pillX + pillWidth, pillY + pillHeight), 35);
        canvas.DrawRoundRect(pillRect, pillPaint);

        // Draw CTA text centered in pill
        float textY = pillY + (pillHeight / 2f) - (textPaint.FontMetrics.Ascent / 2f) - (textPaint.FontMetrics.Descent / 2f);
        canvas.DrawText(ctaText, canvasWidth / 2f, textY, textPaint);
    }

    // ───────────────────────────────────────────────────────────────────────
    //  Helpers
    // ───────────────────────────────────────────────────────────────────────

    private static float ResolveYStart(string? textPosition, int canvasHeight)
    {
        return (textPosition?.ToLowerInvariant() ?? "center") switch
        {
            "top"    => Margin + 80,
            "bottom" => canvasHeight * 0.55f,
            _        => canvasHeight * 0.3f, // center (default)
        };
    }

    private static float ResolveTextX(SKTextAlign align, int canvasWidth)
    {
        return align switch
        {
            SKTextAlign.Center => canvasWidth / 2f,
            SKTextAlign.Right  => canvasWidth - Margin,
            _                  => Margin
        };
    }

    private static SKTextAlign ResolveTextAlign(string? alignment)
    {
        return (alignment?.ToLowerInvariant() ?? "center") switch
        {
            "left"  => SKTextAlign.Left,
            "right" => SKTextAlign.Right,
            _       => SKTextAlign.Center,
        };
    }

    private static SKFontStyleWeight ResolveFontWeight(string? fontWeight)
    {
        return (fontWeight?.ToLowerInvariant() ?? "bold") switch
        {
            "regular"   => SKFontStyleWeight.Normal,
            "medium"    => SKFontStyleWeight.Medium,
            "semibold"  => SKFontStyleWeight.SemiBold,
            "bold"      => SKFontStyleWeight.Bold,
            "extrabold" => SKFontStyleWeight.ExtraBold,
            _           => SKFontStyleWeight.Bold,
        };
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
