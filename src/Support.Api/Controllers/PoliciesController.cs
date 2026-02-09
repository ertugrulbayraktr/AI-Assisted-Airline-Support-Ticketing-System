using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Support.Application.Features.Policies.Commands.CreatePolicy;
using Support.Application.Features.Policies.Commands.PublishPolicy;
using System.Security.Claims;

namespace Support.Api.Controllers;

/// <summary>
/// Policy and knowledge base management (Admin only)
/// </summary>
[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/[controller]")]
public class PoliciesController : ControllerBase
{
    private readonly CreatePolicyHandler _createPolicyHandler;
    private readonly PublishPolicyHandler _publishPolicyHandler;

    public PoliciesController(
        CreatePolicyHandler createPolicyHandler,
        PublishPolicyHandler publishPolicyHandler)
    {
        _createPolicyHandler = createPolicyHandler;
        _publishPolicyHandler = publishPolicyHandler;
    }

    /// <summary>
    /// Create a new policy document (draft state)
    /// </summary>
    /// <param name="command">Policy creation details (title and markdown content)</param>
    /// <returns>Created policy ID</returns>
    /// <response code="201">Policy created successfully</response>
    /// <response code="400">Invalid policy data</response>
    /// <response code="403">User not authorized (must be Admin)</response>
    [HttpPost]
    [ProducesResponseType(typeof(CreatePolicyResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreatePolicy([FromBody] CreatePolicyCommand command)
    {
        // Admin ID must come from JWT claims
        command.AuthorId = GetUserId();

        var result = await _createPolicyHandler.Handle(command, HttpContext.RequestAborted);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.ErrorMessage, errors = result.Errors });
        }

        return CreatedAtAction(
            nameof(GetPolicy), 
            new { id = result.Data!.PolicyId }, 
            result.Data);
    }

    /// <summary>
    /// Publish a policy document (makes it searchable and chunks it for RAG)
    /// </summary>
    /// <param name="id">Policy document ID</param>
    /// <returns>Success status</returns>
    /// <response code="200">Policy published successfully</response>
    /// <response code="400">Invalid request or policy cannot be published</response>
    /// <response code="403">User not authorized (must be Admin)</response>
    /// <response code="404">Policy not found</response>
    [HttpPost("{id}/publish")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PublishPolicy(Guid id)
    {
        var command = new PublishPolicyCommand
        {
            PolicyId = id,
            PublishedByUserId = GetUserId()
        };

        var result = await _publishPolicyHandler.Handle(command, HttpContext.RequestAborted);

        if (!result.IsSuccess)
        {
            if (result.ErrorMessage?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            {
                return NotFound(new { error = result.ErrorMessage });
            }

            return BadRequest(new { error = result.ErrorMessage, errors = result.Errors });
        }

        return Ok(new { message = "Policy published and indexed successfully" });
    }

    /// <summary>
    /// Get policy by ID (placeholder - implement if needed)
    /// </summary>
    /// <param name="id">Policy ID</param>
    /// <returns>Policy details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetPolicy(Guid id)
    {
        // Note: This is a placeholder for CreatedAtAction.
        // Full implementation would require a GetPolicyByIdQuery in Application layer.
        // For now, return a simple response indicating the policy was created.
        return Ok(new { id, message = "Policy retrieval endpoint - implement GetPolicyByIdQuery if needed" });
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}
