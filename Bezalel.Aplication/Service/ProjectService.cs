using Microsoft.Extensions.Logging;
using Bezalel.Aplication.Interface;
using Bezalel.Core.DTOs;
using Bezalel.Core.Entities;
using Bezalel.Core.Interfaces;

namespace Bezalel.Aplication.Service;

/// <summary>
/// Orchestrates project CRUD, enforcing ownership and mapping
/// between domain entities and DTOs.
/// </summary>
public class ProjectService : IProjectService
{
    private readonly IProjectRepository _repository;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(IProjectRepository repository, ILogger<ProjectService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ProjectDetailDto> CreateAsync(CreateProjectRequest request, string userId)
    {
        var project = new Project
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            Title = request.Title,
            Format = request.Format,
            Status = "Draft",
            OriginJobId = request.OriginJobId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repository.SaveAsync(project);

        _logger.LogInformation("[ProjectService] Created project {ProjectId} for user {UserId}.", project.Id, userId);

        return MapToDetail(project);
    }

    public async Task<IEnumerable<ProjectSummaryDto>> GetRecentProjectsAsync(string userId, int take = 4)
    {
        var projects = await _repository.GetRecentByUserIdAsync(userId, take);

        return projects.Select(MapToSummary);
    }

    public async Task<ProjectDetailDto?> GetProjectByIdAsync(string projectId, string userId)
    {
        var project = await _repository.GetByIdAsync(projectId);

        if (project is null || project.UserId != userId)
        {
            _logger.LogWarning("[ProjectService] Project {ProjectId} not found or access denied for user {UserId}.", projectId, userId);
            return null;
        }

        return MapToDetail(project);
    }

    public async Task UpdateEditorStateAsync(string projectId, string userId, UpdateProjectStateDto request)
    {
        // Ownership check — lightweight read before write
        var project = await _repository.GetByIdAsync(projectId);

        if (project is null || project.UserId != userId)
        {
            _logger.LogWarning("[ProjectService] Auto-save rejected: project {ProjectId} not found or not owned by {UserId}.", projectId, userId);
            throw new UnauthorizedAccessException($"Project '{projectId}' not found or access denied.");
        }

        await _repository.UpdateEditorStateAsync(projectId, request.EditorState);

        _logger.LogInformation("[ProjectService] EditorState updated for project {ProjectId}.", projectId);
    }

    public async Task DeleteAsync(string projectId, string userId)
    {
        var project = await _repository.GetByIdAsync(projectId);

        if (project is null || project.UserId != userId)
        {
            _logger.LogWarning("[ProjectService] Delete rejected: project {ProjectId} not found or not owned by {UserId}.", projectId, userId);
            throw new UnauthorizedAccessException($"Project '{projectId}' not found or access denied.");
        }

        await _repository.DeleteAsync(projectId);

        _logger.LogInformation("[ProjectService] Deleted project {ProjectId}.", projectId);
    }

    // ── Mapping Helpers ──────────────────────────────────────────────

    private static ProjectSummaryDto MapToSummary(Project p) => new()
    {
        Id = p.Id,
        Title = p.Title,
        Format = p.Format,
        Status = p.Status,
        ThumbnailUrl = p.ThumbnailUrl,
        UpdatedAt = p.UpdatedAt
    };

    private static ProjectDetailDto MapToDetail(Project p) => new()
    {
        Id = p.Id,
        UserId = p.UserId,
        Title = p.Title,
        Format = p.Format,
        Status = p.Status,
        ThumbnailUrl = p.ThumbnailUrl,
        EditorState = p.EditorState,
        OriginJobId = p.OriginJobId,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt
    };
}
