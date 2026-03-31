using Bezalel.Aplication.ViewModel;
using Bezalel.Core.DTOs;

namespace Bezalel.Aplication.Interface;

/// <summary>
/// Application service for carousel job orchestration.
/// </summary>
public interface ICarouselService
{
    /// <summary>
    /// Creates a new CarouselJob, persists it in DynamoDB, and publishes
    /// a message to SQS for the CopywriterWorker to process.
    /// </summary>
    Task<ResultOperation> CreateAsync(CreateCarouselRequest request, string userId);

    /// <summary>
    /// Retrieves the current state of a carousel job for front-end polling.
    /// </summary>
    Task<CarouselJob?> GetByIdAsync(string jobId);
}
