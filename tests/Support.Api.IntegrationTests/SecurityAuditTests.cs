using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.InMemory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Support.Application.Interfaces;
using Support.Domain.Entities;
using Support.Domain.Enums;
using Support.Infrastructure.Persistence;
using Support.Infrastructure.Services;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Support.Api.IntegrationTests;

/// <summary>
/// Critical security audit tests for production readiness
/// Tests items D, B, E from Release Candidate checklist
/// </summary>
public class SecurityAuditTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private HttpClient _client = null!;
    private IServiceScope _scope = null!;
    private ApplicationDbContext _context = null!;
    private IPasswordHasher _passwordHasher = null!;
    
    private Guid _passengerId;
    private Guid _agentId;
    private Guid _adminId;
    private Guid _testTicketId;
    private string _passengerToken = null!;
    private string _agentToken = null!;
    private string _adminToken = null!;

    public SecurityAuditTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        // Use in-memory database with unique name per test class instance
        var testDbName = $"TestDb_{Guid.NewGuid()}";
        
        var webAppFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Jwt:Secret", "TestSecretKeyThatIsAtLeast32CharactersLongForHS256");
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

                // Add in-memory database with SAME name for API and test context
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase(testDbName);
                });
                
                services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

                // Use mock AI services for tests (no external API dependency)
                var aiDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IAiCopilotClient));
                if (aiDescriptor != null) services.Remove(aiDescriptor);
                services.AddScoped<IAiCopilotClient, MockAiCopilotClient>();

                var searchDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IPolicySearchService));
                if (searchDescriptor != null) services.Remove(searchDescriptor);
                services.AddScoped<IPolicySearchService, PolicySearchService>();
            });
        });

        _client = webAppFactory.CreateClient();
        _scope = webAppFactory.Services.CreateScope();
        _context = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        _passwordHasher = _scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        // Seed test data
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
        // Create all test users manually (InMemory DB is fresh for each test)
        var passenger = new User("passenger@test.com", _passwordHasher.HashPassword("Pass123!"), "Test Passenger", Role.Passenger);
        var agent = new User("agent@test.com", _passwordHasher.HashPassword("Agent123!"), "Test Agent", Role.SupportAgent);
        var admin = new User("admin@test.com", _passwordHasher.HashPassword("Admin123!"), "Test Admin", Role.Admin);

        _context.Users.AddRange(passenger, agent, admin);
        await _context.SaveChangesAsync();

        _passengerId = passenger.Id;
        _agentId = agent.Id;
        _adminId = admin.Id;

        // Create a test ticket
        var ticket = new Ticket(
            "Test ticket for security audit",
            "This is a test ticket",
            TicketCategory.General,
            Priority.P2,
            _passengerId,
            "TEST123",
            "TestPassenger");

        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();

        _testTicketId = ticket.Id;

        // Get tokens
        _passengerToken = await GetTokenAsync("passenger@test.com", "Pass123!");
        _agentToken = await GetTokenAsync("agent@test.com", "Agent123!");
        _adminToken = await GetTokenAsync("admin@test.com", "Admin123!");
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
    // ITEM D: PASSENGER MESSAGE SECURITY
    // ============================================

    [Fact]
    public async Task D1_Passenger_Cannot_Create_Internal_Messages()
    {
        // Arrange: Passenger tries to send isInternal=true
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _passengerToken);

        var maliciousRequest = new
        {
            content = "I'm trying to create an internal note as passenger",
            isInternal = true // MALICIOUS: Passenger shouldn't be able to do this
        };

        // Act: Send request with isInternal=true
        var response = await _client.PostAsJsonAsync($"/api/tickets/{_testTicketId}/messages", maliciousRequest);

        // Assert: Request should succeed (201), but message should NOT be internal
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // Verify in database: Message must be isInternal=false
        var message = await _context.TicketMessages
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync(m => m.TicketId == _testTicketId);

        Assert.NotNull(message);
        Assert.False(message.IsInternal); // CRITICAL: Must be false despite request
        Assert.Equal(_passengerId, message.AuthorUserId);
    }

    [Fact]
    public async Task D2_Agent_Can_Create_Internal_Notes()
    {
        // Arrange: Agent sends isInternal=true
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _agentToken);

        var internalNote = new
        {
            content = "Internal note: Customer seems upset, handle with care",
            isInternal = true
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/tickets/{_testTicketId}/messages", internalNote);

        // Assert: Request succeeds and message IS internal
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var message = await _context.TicketMessages
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync(m => m.TicketId == _testTicketId);

        Assert.NotNull(message);
        Assert.True(message.IsInternal); // Agent CAN create internal notes
        Assert.Equal(_agentId, message.AuthorUserId);
    }

    [Fact]
    public async Task D3_Passenger_Cannot_See_Internal_Notes()
    {
        // Arrange: Create an internal note as agent
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _agentToken);
        await _client.PostAsJsonAsync($"/api/tickets/{_testTicketId}/messages", new
        {
            content = "INTERNAL: Credit card verification failed",
            isInternal = true
        });

        // Act: Passenger views ticket
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _passengerToken);
        var response = await _client.GetAsync($"/api/tickets/{_testTicketId}");

        // Assert: Internal note must NOT be in response
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var ticketDetail = await response.Content.ReadFromJsonAsync<JsonElement>();
        var messages = ticketDetail.GetProperty("messages").EnumerateArray().ToList();
        
        // Passenger should not see internal messages
        foreach (var msg in messages)
        {
            Assert.False(msg.GetProperty("isInternal").GetBoolean());
        }
    }

    // ============================================
    // ITEM B: AUTHORIZATION
    // ============================================

    [Fact]
    public async Task B1_Passenger_Cannot_Access_Agent_Queue()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _passengerToken);

        // Act: Passenger tries to access agent queue
        var response = await _client.GetAsync("/api/agent/queue");

        // Assert: Must return 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task B2_Agent_Cannot_Create_Policy()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _agentToken);

        var policyRequest = new
        {
            title = "Test Policy",
            content = "# Test Content"
        };

        // Act: Agent tries to create policy (Admin only)
        var response = await _client.PostAsJsonAsync("/api/policies", policyRequest);

        // Assert: Must return 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task B3_Admin_Can_Create_And_Publish_Policy()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        var policyRequest = new
        {
            title = "Compensation Policy",
            content = "# Compensation\n\n## Flight Delays\nEligible for compensation"
        };

        // Act: Create policy
        var createResponse = await _client.PostAsJsonAsync("/api/policies", policyRequest);

        // Assert: Admin can create
        Assert.True(createResponse.IsSuccessStatusCode); // 200 or 201
        
        var createResult = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var policyId = createResult.GetProperty("policyId").GetString();

        // Act: Publish policy
        var publishResponse = await _client.PostAsync($"/api/policies/{policyId}/publish", null);

        // Assert: Admin can publish
        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
    }

    // ============================================
    // ITEM E: CLAIMS USAGE (Identity from JWT only)
    // ============================================

    [Fact]
    public async Task E1_Assign_Uses_JWT_Claims_Not_Request_Body()
    {
        // Arrange: Agent assigns ticket
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _agentToken);

        // Malicious: Try to impersonate another user via request body
        var assignRequest = new
        {
            agentId = _agentId // This is OK - target agent
            // Note: assignedByUserId should NEVER come from request body
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/tickets/{_testTicketId}/assign", assignRequest);

        // Assert: Success
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify: The audit event should show the CURRENT agent from JWT as the actor
        var auditEvent = await _context.TicketAuditEvents
            .OrderByDescending(e => e.Timestamp)
            .FirstOrDefaultAsync(e => e.TicketId == _testTicketId && e.EventType == AuditEventType.Assigned);

        Assert.NotNull(auditEvent);
        Assert.Equal(_agentId, auditEvent.ActorId); // Must be from JWT, not request body
    }

    [Fact]
    public async Task E2_Transition_Uses_JWT_Claims_Not_Request_Body()
    {
        // Arrange: Assign first
        var ticket = await _context.Tickets.FindAsync(_testTicketId);
        ticket!.Transition(TicketState.Triaged);
        await _context.SaveChangesAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _agentToken);

        // Act: Transition (userId must come from JWT)
        var transitionRequest = new { newState = 2 }; // Assigned

        var response = await _client.PostAsJsonAsync($"/api/tickets/{_testTicketId}/transition", transitionRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify: Audit event actor is from JWT
        var auditEvent = await _context.TicketAuditEvents
            .OrderByDescending(e => e.Timestamp)
            .FirstOrDefaultAsync(e => e.EventType == AuditEventType.StateChanged);

        Assert.NotNull(auditEvent);
        Assert.Equal(_agentId, auditEvent.ActorId); // From JWT claims
    }

    // ============================================
    // ITEM C: STATE MACHINE CONFLICT (409)
    // ============================================

    [Fact]
    public async Task C1_Invalid_Transition_Returns_409_Conflict()
    {
        // Arrange: Ticket is in New state
        var ticket = await _context.Tickets.FindAsync(_testTicketId);
        Assert.Equal(TicketState.New, ticket!.State);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _agentToken);

        // Act: Try invalid transition New -> Closed (not allowed)
        var invalidTransition = new { newState = 6 }; // Closed

        var response = await _client.PostAsJsonAsync($"/api/tickets/{_testTicketId}/transition", invalidTransition);

        // Assert: Must return 409 Conflict
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var errorContent = await response.Content.ReadAsStringAsync();
        Assert.Contains("transition", errorContent.ToLower());
    }

    [Fact]
    public async Task C2_Valid_Transition_Succeeds()
    {
        // Arrange: Ensure ticket is in New state
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _agentToken);

        // Act: Valid transition New -> Triaged
        var validTransition = new { newState = 1 }; // Triaged

        var response = await _client.PostAsJsonAsync($"/api/tickets/{_testTicketId}/transition", validTransition);

        // Assert: Success
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify state changed in DB (query fresh data, not tracked entity)
        var updatedTicket = await _context.Tickets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == _testTicketId);
        
        Assert.NotNull(updatedTicket);
        Assert.Equal(TicketState.Triaged, updatedTicket.State);
    }
}
