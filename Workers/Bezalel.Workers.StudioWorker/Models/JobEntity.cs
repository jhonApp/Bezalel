namespace Bezalel.Workers.StudioWorker.Models;

/// <summary>Represents a job record stored in DynamoDB.</summary>
public record JobEntity(
    string JobId,
    string CarouselJobId,
    int    SlideCount,
    string Status,
    string? FinalS3Url = null,
    string? ErrorMessage = null
);
