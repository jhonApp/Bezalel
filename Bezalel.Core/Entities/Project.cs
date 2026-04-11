namespace Bezalel.Core.Entities;

/// <summary>
/// Represents a user's design project (Carousel, Post, or Story).
/// The <see cref="EditorState"/> property stores the full canvas JSON payload
/// from the front-end editor — it is intentionally a raw string to keep
/// the domain layer agnostic of the editor's internal schema.
/// </summary>
public class Project
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Owner of the project (maps to the authenticated user).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Human-readable title, e.g. "Carrossel: Lançamento".</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Visual format: "Carousel", "Post", or "Story".</summary>
    public string Format { get; set; } = string.Empty;

    /// <summary>Lifecycle status: "Draft", "Generating", "Completed".</summary>
    public string Status { get; set; } = "Draft";

    /// <summary>S3 URL of the thumbnail shown on the Home grid.</summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// Full JSON payload representing the editor canvas state.
    /// Only loaded when entering the Editor — never returned in list queries.
    /// </summary>
    public string? EditorState { get; set; }

    /// <summary>Optional reference to the CarouselJob that originated this project.</summary>
    public string? OriginJobId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
