using System.Text.Json.Serialization;

namespace Bezalel.Workers.StudioWorker.Models;

/// <summary>
/// Payload deserialized from the SQS message body.
/// Sent by CopywriterWorker after saving the carousel JSON to DynamoDB.
/// </summary>
public record SqsJobPayload(
    [property: JsonPropertyName("jobId")]          string JobId,
    [property: JsonPropertyName("carouselJobId")]  string CarouselJobId
);
