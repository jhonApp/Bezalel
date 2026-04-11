using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bezalel.Aplication.Interface;
using Bezalel.Core.DTOs;
using System.Security.Claims;

namespace Bezalel.API.Controllers;

/// <summary>
/// REST API for project CRUD and auto-save.
/// All endpoints require authentication and enforce ownership via JWT userId.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;

    public ProjectsController(IProjectService projectService)
    {
        _projectService = projectService;
    }

    /// <summary>
    /// POST /api/projects
    /// Creates a new draft project. Returns 201 with the full project detail.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetUserId();
        var detail = await _projectService.CreateAsync(request, userId);

        return CreatedAtAction(nameof(GetById), new { id = detail.Id }, detail);
    }

    /// <summary>
    /// GET /api/projects/recent?take=4
    /// Returns the N most recently updated projects for the Home grid.
    /// Response deliberately excludes EditorState.
    /// </summary>
    [HttpGet("recent")]
    public async Task<IActionResult> GetRecent([FromQuery] int take = 4)
    {
        var userId = GetUserId();
        var projects = await _projectService.GetRecentProjectsAsync(userId, take);

        return Ok(projects);
    }

    /// <summary>
    /// GET /api/projects/{id}
    /// Returns the full project detail, including EditorState, for the Editor.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById([FromRoute] string id)
    {
        var userId = GetUserId();
        var detail = await _projectService.GetProjectByIdAsync(id, userId);

        if (detail is null)
            return NotFound(new { Message = $"Project '{id}' not found." });

        return Ok(detail);
    }

    /// <summary>
    /// PATCH /api/projects/{id}/state
    /// Auto-save endpoint. Accepts only the EditorState JSON payload.
    /// Returns 204 No Content on success.
    /// </summary>
    [HttpPatch("{id}/state")]
    public async Task<IActionResult> UpdateState([FromRoute] string id, [FromBody] UpdateProjectStateDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetUserId();

        try
        {
            await _projectService.UpdateEditorStateAsync(id, userId, request);
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound(new { Message = $"Project '{id}' not found." });
        }
    }

    /// <summary>
    /// DELETE /api/projects/{id}
    /// Deletes a project owned by the authenticated user.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete([FromRoute] string id)
    {
        var userId = GetUserId();

        try
        {
            await _projectService.DeleteAsync(id, userId);
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound(new { Message = $"Project '{id}' not found." });
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private string GetUserId()
    {
        // Extract from JWT claims (NameIdentifier or fallback to Name)
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.Identity?.Name
            ?? "anonymous";
    }
}
