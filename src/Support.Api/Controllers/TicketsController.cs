using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Support.Application.Features.Tickets.Commands.CreateTicket;
using Support.Application.Features.Tickets.Commands.CreateInternalTicket;
using Support.Application.Features.Tickets.Commands.AddMessage;
using Support.Application.Features.Tickets.Commands.AssignTicket;
using Support.Application.Features.Tickets.Commands.TransitionTicket;
using Support.Application.Features.Tickets.Queries.GetMyTickets;
using Support.Application.Features.Tickets.Queries.GetTicketById;
using System.Security.Claims;

namespace Support.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TicketsController : ControllerBase
{
    private readonly CreateTicketHandler _createTicketHandler;
    private readonly CreateInternalTicketHandler _createInternalTicketHandler;
    private readonly AddMessageHandler _addMessageHandler;
    private readonly AssignTicketHandler _assignTicketHandler;
    private readonly TransitionTicketHandler _transitionTicketHandler;
    private readonly GetMyTicketsHandler _getMyTicketsHandler;
    private readonly GetTicketByIdHandler _getTicketByIdHandler;

    public TicketsController(
        CreateTicketHandler createTicketHandler,
        CreateInternalTicketHandler createInternalTicketHandler,
        AddMessageHandler addMessageHandler,
        AssignTicketHandler assignTicketHandler,
        TransitionTicketHandler transitionTicketHandler,
        GetMyTicketsHandler getMyTicketsHandler,
        GetTicketByIdHandler getTicketByIdHandler)
    {
        _createTicketHandler = createTicketHandler;
        _createInternalTicketHandler = createInternalTicketHandler;
        _addMessageHandler = addMessageHandler;
        _assignTicketHandler = assignTicketHandler;
        _transitionTicketHandler = transitionTicketHandler;
        _getMyTicketsHandler = getMyTicketsHandler;
        _getTicketByIdHandler = getTicketByIdHandler;
    }

    [HttpPost]
    [Authorize(Roles = "Passenger")]
    public async Task<IActionResult> CreateTicket([FromBody] CreateTicketCommand command)
    {
        command.UserId = GetUserId();
        var result = await _createTicketHandler.Handle(command, HttpContext.RequestAborted);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(new { error = result.ErrorMessage });
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMyTickets([FromQuery] GetMyTicketsQuery query)
    {
        query.UserId = GetUserId();
        var result = await _getMyTicketsHandler.Handle(query, HttpContext.RequestAborted);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(new { error = result.ErrorMessage });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTicket(Guid id)
    {
        var query = new GetTicketByIdQuery { TicketId = id, RequestingUserId = GetUserId() };
        var result = await _getTicketByIdHandler.Handle(query, HttpContext.RequestAborted);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(new { error = result.ErrorMessage });
    }

    [HttpPost("{id}/messages")]
    public async Task<IActionResult> AddMessage(Guid id, [FromBody] AddMessageCommand command)
    {
        command.TicketId = id;
        command.UserId = GetUserId();
        var result = await _addMessageHandler.Handle(command, HttpContext.RequestAborted);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(new { error = result.ErrorMessage });
    }

    // ===== AGENT/ADMIN OPERATIONS =====

    /// <summary>
    /// Create internal ticket (Agent/Admin only, PNR optional)
    /// </summary>
    /// <param name="command">Internal ticket creation details</param>
    /// <returns>Created ticket ID and number</returns>
    /// <response code="201">Ticket created successfully</response>
    /// <response code="400">Invalid request data</response>
    /// <response code="403">User not authorized (must be Agent or Admin)</response>
    [HttpPost("internal")]
    [Authorize(Roles = "SupportAgent,Admin")]
    [ProducesResponseType(typeof(CreateInternalTicketResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateInternalTicket([FromBody] CreateInternalTicketCommand command)
    {
        // Agent/Admin ID must come from JWT claims, not request body
        command.UserId = GetUserId();
        
        var result = await _createInternalTicketHandler.Handle(command, HttpContext.RequestAborted);
        
        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.ErrorMessage, errors = result.Errors });
        }

        // Return 201 Created with location header
        return CreatedAtAction(
            nameof(GetTicket), 
            new { id = result.Data!.TicketId }, 
            result.Data);
    }

    /// <summary>
    /// Assign ticket to an agent (Agent/Admin only)
    /// </summary>
    /// <param name="id">Ticket ID</param>
    /// <param name="request">Assignment request with target agent ID</param>
    /// <returns>Success status</returns>
    /// <response code="200">Ticket assigned successfully</response>
    /// <response code="400">Invalid request or assignment not allowed</response>
    /// <response code="403">User not authorized</response>
    /// <response code="404">Ticket or agent not found</response>
    [HttpPost("{id}/assign")]
    [Authorize(Roles = "SupportAgent,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignTicket(Guid id, [FromBody] AssignTicketRequest request)
    {
        var command = new AssignTicketCommand
        {
            TicketId = id,
            AgentId = request.AgentId,
            AssignedByUserId = GetUserId() // Current user performing the assignment
        };

        var result = await _assignTicketHandler.Handle(command, HttpContext.RequestAborted);

        if (!result.IsSuccess)
        {
            // Check for specific error types
            if (result.ErrorMessage?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            {
                return NotFound(new { error = result.ErrorMessage });
            }
            
            return BadRequest(new { error = result.ErrorMessage, errors = result.Errors });
        }

        return Ok(new { message = "Ticket assigned successfully" });
    }

    /// <summary>
    /// Transition ticket to a new state (Agent/Admin only)
    /// </summary>
    /// <param name="id">Ticket ID</param>
    /// <param name="request">State transition request</param>
    /// <returns>Success status</returns>
    /// <response code="200">State transition successful</response>
    /// <response code="400">Invalid state transition</response>
    /// <response code="403">User not authorized</response>
    /// <response code="404">Ticket not found</response>
    /// <response code="409">Transition not allowed by state machine rules</response>
    [HttpPost("{id}/transition")]
    [Authorize(Roles = "SupportAgent,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> TransitionTicket(Guid id, [FromBody] TransitionTicketRequest request)
    {
        var command = new TransitionTicketCommand
        {
            TicketId = id,
            NewState = request.NewState,
            UserId = GetUserId() // Agent performing the transition
        };

        var result = await _transitionTicketHandler.Handle(command, HttpContext.RequestAborted);

        if (!result.IsSuccess)
        {
            // Detect state machine violation (409 Conflict)
            if (result.ErrorMessage?.Contains("Invalid state transition", StringComparison.OrdinalIgnoreCase) == true ||
                result.ErrorMessage?.Contains("transition", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Conflict(new { error = result.ErrorMessage });
            }

            if (result.ErrorMessage?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            {
                return NotFound(new { error = result.ErrorMessage });
            }

            return BadRequest(new { error = result.ErrorMessage, errors = result.Errors });
        }

        return Ok(new { message = "Ticket state transitioned successfully" });
    }

    /// <summary>
    /// Add internal note to ticket (Agent/Admin only, hidden from passengers)
    /// </summary>
    /// <param name="id">Ticket ID</param>
    /// <param name="request">Internal note content</param>
    /// <returns>Created note ID</returns>
    [HttpPost("{id}/internal-notes")]
    [Authorize(Roles = "SupportAgent,Admin")]
    [ProducesResponseType(typeof(AddMessageResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddInternalNote(Guid id, [FromBody] AddInternalNoteRequest request)
    {
        var command = new AddMessageCommand
        {
            TicketId = id,
            Content = request.Content,
            IsInternal = true, // Force internal = true for this endpoint
            UserId = GetUserId()
        };

        var result = await _addMessageHandler.Handle(command, HttpContext.RequestAborted);
        
        return result.IsSuccess 
            ? CreatedAtAction(nameof(GetTicket), new { id }, result.Data)
            : BadRequest(new { error = result.ErrorMessage });
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}

// ===== REQUEST DTOs FOR API ENDPOINTS =====
// These are API-specific models (not Application layer models)
// They adapt HTTP requests to Application Commands

/// <summary>
/// Request model for assigning a ticket to an agent
/// </summary>
public record AssignTicketRequest
{
    /// <summary>
    /// ID of the agent to assign the ticket to
    /// </summary>
    public Guid AgentId { get; init; }
}

/// <summary>
/// Request model for transitioning ticket state
/// </summary>
public record TransitionTicketRequest
{
    /// <summary>
    /// Target state for the ticket (0=New, 1=Triaged, 2=Assigned, 3=InProgress, 4=WaitingOnPassenger, 5=Resolved, 6=Closed, 7=Cancelled)
    /// </summary>
    public Support.Domain.Enums.TicketState NewState { get; init; }
}

/// <summary>
/// Request model for adding internal note
/// </summary>
public record AddInternalNoteRequest
{
    /// <summary>
    /// Internal note content (visible only to agents/admins)
    /// </summary>
    public string Content { get; init; } = null!;
}
