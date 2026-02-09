using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Support.Application.Features.Tickets.Queries.GetAgentQueue;
using System.Security.Claims;

namespace Support.Api.Controllers;

/// <summary>
/// Agent-specific operations for ticket management and queue handling
/// </summary>
[Authorize(Roles = "SupportAgent,Admin")]
[ApiController]
[Route("api/[controller]")]
public class AgentController : ControllerBase
{
    private readonly GetAgentQueueHandler _getAgentQueueHandler;

    public AgentController(GetAgentQueueHandler getAgentQueueHandler)
    {
        _getAgentQueueHandler = getAgentQueueHandler;
    }

    /// <summary>
    /// Get agent ticket queue with filtering and paging
    /// </summary>
    /// <param name="query">Queue filters including state, priority, category, SLA risk, pagination</param>
    /// <returns>Paginated list of tickets in agent queue</returns>
    /// <response code="200">Returns the filtered ticket queue</response>
    /// <response code="400">If query parameters are invalid</response>
    [HttpGet("queue")]
    [ProducesResponseType(typeof(GetAgentQueueResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetQueue([FromQuery] GetAgentQueueQuery query)
    {
        // Agent can optionally filter by their own assignments
        // If FilterByAssignedToMe is provided as true in query string, use current agent's ID
        // Otherwise, query.FilterByAssignedToMe will be null (show all tickets)
        
        var result = await _getAgentQueueHandler.Handle(query, HttpContext.RequestAborted);
        
        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.ErrorMessage, errors = result.Errors });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Get agent's own ticket queue (assigned to current agent only)
    /// </summary>
    /// <param name="pageNumber">Page number (default 1)</param>
    /// <param name="pageSize">Page size (default 20)</param>
    /// <returns>Paginated list of tickets assigned to current agent</returns>
    [HttpGet("my-queue")]
    [ProducesResponseType(typeof(GetAgentQueueResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyQueue([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        var agentId = GetUserId();
        
        var query = new GetAgentQueueQuery
        {
            FilterByAssignedToMe = agentId,
            PageNumber = pageNumber,
            PageSize = pageSize,
            SortBy = "Priority", // Prioritize by urgency
            SortDescending = true
        };

        var result = await _getAgentQueueHandler.Handle(query, HttpContext.RequestAborted);
        
        return result.IsSuccess 
            ? Ok(result.Data) 
            : BadRequest(new { error = result.ErrorMessage });
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}
