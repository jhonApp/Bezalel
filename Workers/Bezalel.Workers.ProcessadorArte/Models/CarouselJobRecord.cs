using System.Text.Json.Serialization;

namespace Bezalel.Workers.ProcessadorArte.Models;

/// <summary>
/// Read model returned by the repository containing everything
/// the ProcessadorArte pipeline needs to render carousel slides.
/// </summary>
public record CarouselJobRecord(
    [property: JsonPropertyName("jobId")]             string          JobId,
    [property: JsonPropertyName("backgroundPrompt")]  string          BackgroundPrompt,
    [property: JsonPropertyName("palette")]           PaletteRecord   Palette,
    [property: JsonPropertyName("slides")]            List<SlideRecord> Slides
);

public record PaletteRecord(
    [property: JsonPropertyName("backgroundColor")]   string BackgroundColor,
    [property: JsonPropertyName("primaryTextColor")]   string PrimaryTextColor,
    [property: JsonPropertyName("accentColor")]        string AccentColor
);

public record SlideRecord(
    [property: JsonPropertyName("order")]              int    Order,
    [property: JsonPropertyName("headline")]           string Headline,
    [property: JsonPropertyName("body")]               string? Body,
    [property: JsonPropertyName("callToAction")]       string? CallToAction,
    [property: JsonPropertyName("slideBackgroundPrompt")] string SlideBackgroundPrompt,
    [property: JsonPropertyName("layout")]             SlideLayoutRecord Layout
);

public record SlideLayoutRecord(
    [property: JsonPropertyName("textPosition")]       string TextPosition,
    [property: JsonPropertyName("textAlignment")]      string TextAlignment,
    [property: JsonPropertyName("headlineFontSize")]   int    HeadlineFontSize,
    [property: JsonPropertyName("bodyFontSize")]       int    BodyFontSize,
    [property: JsonPropertyName("headlineColor")]      string HeadlineColor,
    [property: JsonPropertyName("bodyColor")]          string BodyColor,
    [property: JsonPropertyName("headlineFontWeight")] string HeadlineFontWeight
);
