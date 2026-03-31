using Bezalel.Core.DTOs;

namespace Bezalel.Core.Interfaces;

/// <summary>
/// Repository contract for CarouselJob records in DynamoDB.
/// Used by the API to create jobs and poll status, and by Workers
/// to update the job as it progresses through the pipeline.
/// </summary>
public interface ICarouselJobRepository
{
    /// <summary>
    /// Creates a new CarouselJob record with Status = "PENDING".
    /// Called by the CarouselController when a new request arrives.
    /// </summary>
    Task SaveAsync(CarouselJob job);

    /// <summary>
    /// Retrieves the full CarouselJob record by JobId.
    /// Returns null if not found.
    /// </summary>
    Task<CarouselJob?> GetByIdAsync(string jobId);

    /// <summary>
    /// Updates the status of an existing job (e.g., PENDING → COPYWRITING → RENDERING → COMPLETED).
    /// Optionally sets additional data like slides JSON or final image URLs.
    /// </summary>
    Task UpdateStatusAsync(string jobId, string status, string? slidesJson = null, List<string>? finalImageUrls = null);
}
