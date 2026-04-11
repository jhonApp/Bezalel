using System.Text.Json.Serialization;

namespace Bezalel.Core.DTOs;

/// <summary>
/// Lightweight DTO for the Home grid — deliberately excludes EditorState
/// to keep the list API response small and fast.
/// </summary>
public record ProjectSummaryDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("format")]
    public string Format { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = "Draft";

    [JsonPropertyName("thumbnailUrl")]
    public string? ThumbnailUrl { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Full DTO for the Editor — includes the heavy EditorState JSON payload.
/// Only used on GET /api/projects/{id}.
/// </summary>
public record ProjectDetailDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("userId")]
    public string UserId { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("format")]
    public string Format { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = "Draft";

    [JsonPropertyName("thumbnailUrl")]
    public string? ThumbnailUrl { get; init; }

    [JsonPropertyName("editorState")]
    public string? EditorState { get; init; }

    [JsonPropertyName("originJobId")]
    public string? OriginJobId { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Thin request body for the auto-save endpoint (PATCH).
/// Contains only the EditorState string to minimize payload size.
/// </summary>
public record UpdateProjectStateDto
{
    [JsonPropertyName("editorState")]
    public string EditorState { get; init; } = string.Empty;
}

/// <summary>
/// Request body for creating a new project from the Home page.
/// </summary>
public record CreateProjectRequest
{
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("format")]
    public string Format { get; init; } = string.Empty;

    /// <summary>Optional CarouselJob ID to link this project to an AI generation.</summary>
    [JsonPropertyName("originJobId")]
    public string? OriginJobId { get; init; }
}
