using Bezalel.Core.DTOs;

namespace Bezalel.Aplication.Interface;

/// <summary>
/// Application service for project CRUD and auto-save orchestration.
/// Separates "summary" reads (Home grid) from "detail" reads (Editor)
/// to avoid transferring heavy EditorState payloads on list queries.
/// </summary>
public interface IProjectService
{
    /// <summary>
    /// Creates a new draft project and returns its summary.
    /// </summary>
    Task<ProjectDetailDto> CreateAsync(CreateProjectRequest request, string userId);

    /// <summary>
    /// Returns the most recent projects for the Home grid.
    /// Excludes EditorState to keep the response lightweight.
    /// </summary>
    Task<IEnumerable<ProjectSummaryDto>> GetRecentProjectsAsync(string userId, int take = 4);

    /// <summary>
    /// Returns the full project detail including EditorState for the Editor page.
    /// Throws if the project is not found or does not belong to the user.
    /// </summary>
    Task<ProjectDetailDto?> GetProjectByIdAsync(string projectId, string userId);

    /// <summary>
    /// Auto-save endpoint: updates only the EditorState and bumps UpdatedAt.
    /// Uses a targeted DynamoDB UpdateItem to avoid a full read-modify-write.
    /// </summary>
    Task UpdateEditorStateAsync(string projectId, string userId, UpdateProjectStateDto request);

    /// <summary>
    /// Deletes a project owned by the given user.
    /// </summary>
    Task DeleteAsync(string projectId, string userId);
}
