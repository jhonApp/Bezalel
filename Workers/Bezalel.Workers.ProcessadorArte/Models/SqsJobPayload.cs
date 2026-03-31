using System.Text.Json.Serialization;

namespace Bezalel.Workers.ProcessadorArte.Models;

/// <summary>Payload deserialized from the SQS message body.</summary>
public record SqsJobPayload(
    [property: JsonPropertyName("JobId")]             string JobId,
    [property: JsonPropertyName("CarouselJobId")]     string CarouselJobId,
    [property: JsonPropertyName("SlideOrder")]        int    SlideOrder,
    [property: JsonPropertyName("BackgroundPrompt")]  string BackgroundPrompt,
    [property: JsonPropertyName("SlidesJson")]        string SlidesJson
);
