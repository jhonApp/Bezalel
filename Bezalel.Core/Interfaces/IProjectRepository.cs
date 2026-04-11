using Bezalel.Core.Entities;

namespace Bezalel.Core.Interfaces;

/// <summary>
/// Repository contract for Project records in DynamoDB.
/// Provides both "summary" reads (Home grid) and "full" reads (Editor),
/// plus targeted partial updates for auto-save.
/// </summary>
public interface IProjectRepository
{
    /// <summary>
    /// Persists a new Project record.
    /// </summary>
    Task SaveAsync(Project project);

    /// <summary>
    /// Returns the full Project record by Id.
    /// Returns null if not found.
    /// </summary>
    Task<Project?> GetByIdAsync(string projectId);

    /// <summary>
    /// Returns the N most recently updated projects for a given user,
    /// ordered by UpdatedAt descending.
    /// Only fetches attributes needed for <see cref="DTOs.ProjectSummaryDto"/>.
    /// </summary>
    Task<IReadOnlyList<Project>> GetRecentByUserIdAsync(string userId, int take = 4);

    /// <summary>
    /// Performs a partial update: sets EditorState and bumps UpdatedAt.
    /// Uses UpdateItem to avoid a full read-modify-write cycle.
    /// </summary>
    Task UpdateEditorStateAsync(string projectId, string editorState);

    /// <summary>
    /// Deletes a project by Id (soft or hard, depending on implementation).
    /// </summary>
    Task DeleteAsync(string projectId);
}
