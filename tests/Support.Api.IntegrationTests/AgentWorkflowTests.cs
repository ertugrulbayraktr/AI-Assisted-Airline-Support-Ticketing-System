using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Support.Application.Interfaces;
using Support.Domain.Entities;
using Support.Domain.Enums;
using Support.Infrastructure.Persistence;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Support.Api.IntegrationTests;

/// <summary>
/// End-to-end agent workflow tests
/// Tests item A from Release Candidate checklist
/// </summary>
public class AgentWorkflowTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private HttpClient _client = null!;
    private IServiceScope _scope = null!;
    private ApplicationDbContext _context = null!;
    private IPasswordHasher _passwordHasher = null!;
    
    private Guid _passengerId;
    private Guid _agentId;
    private Guid _testTicketId;
    private string _agentToken = null!;

    public AgentWorkflowTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        var testDbName = $"TestDb_{Guid.NewGuid()}";
        
        var webAppFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove existing DbContext registration
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Remove IApplicationDbContext registration
                var appDbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IApplicationDbContext));
                if (appDbDescriptor != null)
                {
                    services.Remove(appDbDescriptor);
                }

                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase(testDbName);
                });
                
                services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
            });
        });

        _client = webAppFactory.CreateClient();
        _scope = webAppFactory.Services.CreateScope();
        _context = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        _passwordHasher = _scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        await SeedTestDataAsync();
    }

    public async Task DisposeAsync()
    {
        _scope?.Dispose();
        _client?.Dispose();
        await Task.CompletedTask;
    }

    private async Task SeedTestDataAsync()
    {
        // Create test users manually
        var passenger = new User("passenger@test.com", _passwordHasher.HashPassword("Pass123!"), "Test Passenger", Role.Passenger);
        var agent = new User("agent@test.com", _passwordHasher.HashPassword("Agent123!"), "Test Agent", Role.SupportAgent);

        _context.Users.AddRange(passenger, agent);
        await _context.SaveChangesAsync();

        _passengerId = passenger.Id;
        _agentId = agent.Id;

        var ticket = new Ticket(
            "Need help with refund",
            "Please process my refund request",
            TicketCategory.Refund,
            Priority.P2,
            _passengerId,
            "ABC123",
            "TestPassenger");

        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();

        _testTicketId = ticket.Id;

        _agentToken = await GetTokenAsync("agent@test.com", "Agent123!");
    }

    private async Task<string> GetTokenAsync(string email, string password)
    {
        var loginRequest = new
        {
            email = email,
            password = password
        };
        
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Login failed for {email}: {response.StatusCode}, {errorContent}");
        }
        
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        return result.GetProperty("token").GetString()!;
    }

    // ============================================
    // ITEM A: AGENT E2E FLOW
    // ============================================

    [Fact]
    public async Task A_Complete_Agent_Workflow_EndToEnd()
    {
        // Step 1: Agent login -> token (already done in setup)
        Assert.NotNull(_agentToken);
        Assert.NotEmpty(_agentToken);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _agentToken);

        // Step 2: GET /api/agent/queue shows passenger ticket
        var queueResponse = await _client.GetAsync("/api/agent/queue?pageNumber=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, queueResponse.StatusCode);

        var queueResult = await queueResponse.Content.ReadFromJsonAsync<JsonElement>();
        var tickets = queueResult.GetProperty("tickets").EnumerateArray().ToList();
        
        Assert.NotEmpty(tickets);
        var foundTicket = tickets.FirstOrDefault(t => t.GetProperty("id").GetGuid() == _testTicketId);
        Assert.True(foundTicket.ValueKind != JsonValueKind.Undefined, "Ticket should appear in agent queue");

        // Step 3: POST /api/tickets/{id}/assign works
        var assignRequest = new { agentId = _agentId };
        var assignResponse = await _client.PostAsJsonAsync($"/api/tickets/{_testTicketId}/assign", assignRequest);
        Assert.Equal(HttpStatusCode.OK, assignResponse.StatusCode);

        // Verify assignment in DB (fresh query)
        var ticketAfterAssign = await _context.Tickets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == _testTicketId);
        
        Assert.NotNull(ticketAfterAssign);
        Assert.Equal(_agentId, ticketAfterAssign.AssignedToAgentId);
        Assert.Equal(TicketState.Assigned, ticketAfterAssign.State); // Should auto-transition

        // Step 4: POST /api/tickets/{id}/transition to InProgress works
        var transitionToInProgress = new { newState = 3 }; // InProgress
        var inProgressResponse = await _client.PostAsJsonAsync($"/api/tickets/{_testTicketId}/transition", transitionToInProgress);
        Assert.Equal(HttpStatusCode.OK, inProgressResponse.StatusCode);

        var ticketInProgress = await _context.Tickets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == _testTicketId);
        
        Assert.NotNull(ticketInProgress);
        Assert.Equal(TicketState.InProgress, ticketInProgress.State);

        // Step 5: POST /api/tickets/{id}/transition to Resolved works
        var transitionToResolved = new { newState = 5 }; // Resolved
        var resolvedResponse = await _client.PostAsJsonAsync($"/api/tickets/{_testTicketId}/transition", transitionToResolved);
        Assert.Equal(HttpStatusCode.OK, resolvedResponse.StatusCode);

        var ticketResolved = await _context.Tickets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == _testTicketId);
        
        Assert.NotNull(ticketResolved);
        Assert.Equal(TicketState.Resolved, ticketResolved.State);
        Assert.NotNull(ticketResolved.ResolvedAt); // Should have resolved timestamp

        // Step 6: GET /api/tickets/{id} reflects all changes
        var finalResponse = await _client.GetAsync($"/api/tickets/{_testTicketId}");
        Assert.Equal(HttpStatusCode.OK, finalResponse.StatusCode);

        var finalTicket = await finalResponse.Content.ReadFromJsonAsync<JsonElement>();
        
        Assert.Equal(_agentId.ToString(), finalTicket.GetProperty("assignedToAgentId").GetGuid().ToString());
        Assert.Equal(5, finalTicket.GetProperty("state").GetInt32()); // Resolved
        Assert.True(finalTicket.GetProperty("resolvedAt").GetString() != null);
    }
}
