using System.Text.Json.Serialization;

namespace Bezalel.Core.DTOs;

/// <summary>
/// Represents a complete carousel generation job.
/// Created by the API when the user submits a prompt.
/// Enriched by the CopywriterWorker (LLM) with slides and palette.
/// Consumed by ProcessadorArte to render each slide image.
/// </summary>
public record CarouselJob
{
    [JsonPropertyName("jobId")]
    public string JobId { get; init; } = Guid.NewGuid().ToString();

    [JsonPropertyName("userId")]
    public string UserId { get; init; } = string.Empty;

    [JsonPropertyName("tema")]
    public string Tema { get; init; } = string.Empty;

    [JsonPropertyName("backgroundPrompt")]
    public string BackgroundPrompt { get; init; } = string.Empty;

    [JsonPropertyName("palette")]
    public ColorPalette Palette { get; init; } = new();

    [JsonPropertyName("slides")]
    public List<CarouselSlide> Slides { get; init; } = new();

    [JsonPropertyName("status")]
    public string Status { get; init; } = "PENDING";

    [JsonPropertyName("finalImageUrls")]
    public List<string> FinalImageUrls { get; init; } = new();

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Semantic color palette — each color has a clear purpose for the SkiaSharp renderer.
/// </summary>
public record ColorPalette
{
    /// <summary>Base/dominant background color, e.g. "#1A1A2E".</summary>
    [JsonPropertyName("backgroundColor")]
    public string BackgroundColor { get; init; } = "#1A1A2E";

    /// <summary>Primary text/headline color for maximum legibility, e.g. "#FFFFFF".</summary>
    [JsonPropertyName("primaryTextColor")]
    public string PrimaryTextColor { get; init; } = "#FFFFFF";

    /// <summary>Accent color for CTAs, highlights, and decorative elements, e.g. "#E94560".</summary>
    [JsonPropertyName("accentColor")]
    public string AccentColor { get; init; } = "#E94560";
}

/// <summary>
/// Represents a single slide within the carousel.
/// </summary>
public record CarouselSlide
{
    /// <summary>1-based order within the carousel.</summary>
    [JsonPropertyName("order")]
    public int Order { get; init; }

    /// <summary>Main headline text of the slide.</summary>
    [JsonPropertyName("headline")]
    public string Headline { get; init; } = string.Empty;

    /// <summary>Optional body/subtext for content slides.</summary>
    [JsonPropertyName("body")]
    public string? Body { get; init; }

    /// <summary>Optional CTA text (e.g. "Saiba Mais", "Compre Agora"). Required on the last slide.</summary>
    [JsonPropertyName("callToAction")]
    public string? CallToAction { get; init; }

    /// <summary>Per-slide background prompt override for Fal.ai.</summary>
    [JsonPropertyName("slideBackgroundPrompt")]
    public string SlideBackgroundPrompt { get; init; } = string.Empty;

    /// <summary>Layout instructions for SkiaSharp rendering.</summary>
    [JsonPropertyName("layout")]
    public SlideLayout Layout { get; init; } = new();
}

/// <summary>
/// Deterministic layout instructions consumed by the SkiaSharp renderer
/// to position and style text on each slide.
/// </summary>
public record SlideLayout
{
    /// <summary>Vertical text position: "top", "center", "bottom".</summary>
    [JsonPropertyName("textPosition")]
    public string TextPosition { get; init; } = "center";

    /// <summary>Horizontal text alignment: "left", "center", "right".</summary>
    [JsonPropertyName("textAlignment")]
    public string TextAlignment { get; init; } = "center";

    /// <summary>Headline font size in pixels (1080px reference canvas).</summary>
    [JsonPropertyName("headlineFontSize")]
    public int HeadlineFontSize { get; init; } = 72;

    /// <summary>Body font size in pixels (1080px reference canvas).</summary>
    [JsonPropertyName("bodyFontSize")]
    public int BodyFontSize { get; init; } = 36;

    /// <summary>Headline color (hex override, or falls back to palette PrimaryTextColor).</summary>
    [JsonPropertyName("headlineColor")]
    public string HeadlineColor { get; init; } = "#FFFFFF";

    /// <summary>Body text color (hex override).</summary>
    [JsonPropertyName("bodyColor")]
    public string BodyColor { get; init; } = "#E0E0E0";

    /// <summary>Headline font weight: "regular", "medium", "semibold", "bold", "extrabold".</summary>
    [JsonPropertyName("headlineFontWeight")]
    public string HeadlineFontWeight { get; init; } = "bold";
}
