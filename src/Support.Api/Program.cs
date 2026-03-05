using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Support.Api.Filters;
using Support.Application.Interfaces;
using Support.Infrastructure.BackgroundServices;
using Support.Infrastructure.Persistence;
using Support.Infrastructure.Services;
using System.Text;

// Handlers
using Support.Application.Features.Auth.Commands.Login;
using Support.Application.Features.Auth.Commands.VerifyPnr;
using Support.Application.Features.Tickets.Commands.CreateTicket;
using Support.Application.Features.Tickets.Commands.AddMessage;
using Support.Application.Features.Tickets.Queries.GetMyTickets;
using Support.Application.Features.Tickets.Queries.GetTicketById;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IApplicationDbContext>(provider => 
    provider.GetRequiredService<ApplicationDbContext>());

// Services
builder.Services.AddScoped<IReservationProvider, MockReservationProvider>();

// AI services: use Gemini if API key is configured, otherwise fall back to mock
if (!string.IsNullOrEmpty(builder.Configuration["Gemini:ApiKey"]))
{
    builder.Services.AddScoped<IAiCopilotClient, GeminiCopilotClient>();
    builder.Services.AddScoped<IPolicySearchService, GeminiEmbeddingPolicySearchService>();
}
else
{
    builder.Services.AddScoped<IAiCopilotClient, MockAiCopilotClient>();
    builder.Services.AddScoped<IPolicySearchService, PolicySearchService>();
}

builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();

// Handlers - Auth
builder.Services.AddScoped<LoginHandler>();
builder.Services.AddScoped<VerifyPnrHandler>();

// Handlers - Tickets (Passenger)
builder.Services.AddScoped<CreateTicketHandler>();
builder.Services.AddScoped<AddMessageHandler>();
builder.Services.AddScoped<GetMyTicketsHandler>();
builder.Services.AddScoped<GetTicketByIdHandler>();

// Handlers - Tickets (Agent)
builder.Services.AddScoped<Support.Application.Features.Tickets.Commands.CreateInternalTicket.CreateInternalTicketHandler>();
builder.Services.AddScoped<Support.Application.Features.Tickets.Commands.AssignTicket.AssignTicketHandler>();
builder.Services.AddScoped<Support.Application.Features.Tickets.Commands.TransitionTicket.TransitionTicketHandler>();
builder.Services.AddScoped<Support.Application.Features.Tickets.Queries.GetAgentQueue.GetAgentQueueHandler>();

// Handlers - Policies (Admin)
builder.Services.AddScoped<Support.Application.Features.Policies.Commands.CreatePolicy.CreatePolicyHandler>();
builder.Services.AddScoped<Support.Application.Features.Policies.Commands.PublishPolicy.PublishPolicyHandler>();
builder.Services.AddScoped<Support.Application.Features.Policies.Queries.GetPolicyById.GetPolicyByIdHandler>();

// Background Services
builder.Services.AddHostedService<SlaMonitorService>();

// JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("JWT secret is not configured. Set 'Jwt:Secret' via user-secrets or environment variable.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "AirlineSupport",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "AirlineSupport",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization();

// Validation
builder.Services.AddValidatorsFromAssemblyContaining<Support.Application.Features.Auth.Commands.Login.LoginCommandValidator>();
builder.Services.AddScoped<ValidationFilter>();

// Controllers
builder.Services.AddControllers(options =>
{
    options.Filters.AddService<ValidationFilter>();
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Airline Support API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Seed database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    await Support.Infrastructure.Persistence.DbSeeder.SeedAsync(context, passwordHasher);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred." });
    });
});

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Make Program class accessible for integration tests
public partial class Program { }
