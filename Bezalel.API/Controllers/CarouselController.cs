using Microsoft.AspNetCore.Mvc;
using Bezalel.Aplication.Interface;
using Bezalel.Core.DTOs;

namespace Bezalel.API.Controllers;

/// <summary>
/// API Controller for the Carousel domain.
/// Handles creation of carousel jobs and status polling.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CarouselController : ControllerBase
{
    private readonly ICarouselService _carouselService;

    public CarouselController(ICarouselService carouselService)
    {
        _carouselService = carouselService;
    }

    /// <summary>
    /// POST /api/carousel
    /// Receives the user's creative prompt, creates a CarouselJob,
    /// publishes to SQS for the CopywriterWorker, and returns HTTP 202.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCarouselRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // In a real app, extract UserId from JWT Claims
        var userId = User.Identity?.Name ?? "anonymous";

        var result = await _carouselService.CreateAsync(request, userId);

        if (!result.Success)
            return BadRequest(new { result.Message });

        return Accepted(result.Data);
    }

    /// <summary>
    /// GET /api/carousel/{jobId}
    /// Polling endpoint for the front-end to check carousel generation progress.
    /// Returns the full CarouselJob with status, slides, and final image URLs when available.
    /// </summary>
    [HttpGet("{jobId}")]
    public async Task<IActionResult> GetStatus([FromRoute] string jobId)
    {
        var job = await _carouselService.GetByIdAsync(jobId);

        if (job is null)
            return NotFound(new { Message = $"Carousel job '{jobId}' not found." });

        return Ok(new
        {
            job.JobId,
            job.Status,
            job.Tema,
            job.Palette,
            SlideCount = job.Slides.Count,
            job.Slides,
            job.FinalImageUrls,
            job.CreatedAt
        });
    }
}
