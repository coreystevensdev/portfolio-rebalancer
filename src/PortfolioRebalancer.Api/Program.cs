using System.Security.Claims;
using System.Text;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PortfolioRebalancer.Api.Data;
using PortfolioRebalancer.Api.Features.Portfolios;
using PortfolioRebalancer.Api.Features.Rebalancing;
using PortfolioRebalancer.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

var jwtSecret = builder.Configuration["JWT_SECRET"]
    ?? throw new InvalidOperationException("JWT_SECRET is required.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero,
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")
        ?? throw new InvalidOperationException("Connection string 'Postgres' is required.")));

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Portfolio Rebalancer API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "JWT bearer token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithTags("Health")
    .AllowAnonymous();

var api = app.MapGroup("/api").RequireAuthorization();

// Portfolios
api.MapPost("/portfolios", async (
    CreatePortfolioRequestBody body,
    ClaimsPrincipal user,
    IMediator mediator,
    CancellationToken ct) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("Missing sub claim.");

    var cmd = new CreatePortfolioCommand(userId, body.Name, body.DriftTolerancePct, body.Allocations);
    var result = await mediator.Send(cmd, ct);
    return Results.Created($"/api/portfolios/{result.Id}", result);
}).WithTags("Portfolios");

api.MapGet("/portfolios/{id:guid}", async (
    Guid id,
    ClaimsPrincipal user,
    IMediator mediator,
    CancellationToken ct) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("Missing sub claim.");

    var result = await mediator.Send(new GetPortfolioQuery(userId, id), ct);
    return result is null ? Results.NotFound() : Results.Ok(result);
}).WithTags("Portfolios");

api.MapPost("/portfolios/{id:guid}/holdings", async (
    Guid id,
    AddHoldingRequestBody body,
    ClaimsPrincipal user,
    IMediator mediator,
    CancellationToken ct) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("Missing sub claim.");

    var cmd = new AddHoldingCommand(userId, id, body.Ticker, body.Lots);
    var result = await mediator.Send(cmd, ct);
    return Results.Created($"/api/portfolios/{id}/holdings/{result.HoldingId}", result);
}).WithTags("Portfolios");

// Rebalancing
api.MapPost("/portfolios/{id:guid}/drift", async (
    Guid id,
    PricesBody body,
    ClaimsPrincipal user,
    IMediator mediator,
    CancellationToken ct) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("Missing sub claim.");

    var result = await mediator.Send(new GetDriftQuery(userId, id, body.Prices), ct);
    return Results.Ok(result);
}).WithTags("Rebalancing");

api.MapPost("/portfolios/{id:guid}/rebalance", async (
    Guid id,
    PricesBody body,
    ClaimsPrincipal user,
    IMediator mediator,
    CancellationToken ct) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("Missing sub claim.");

    var result = await mediator.Send(new RebalanceCommand(userId, id, body.Prices), ct);
    return Results.Created($"/api/portfolios/{id}/rebalance/{result.EventId}", result);
}).WithTags("Rebalancing");

api.MapGet("/portfolios/{id:guid}/rebalance", async (
    Guid id,
    ClaimsPrincipal user,
    IMediator mediator,
    CancellationToken ct) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("Missing sub claim.");

    var result = await mediator.Send(new GetHistoryQuery(userId, id), ct);
    return Results.Ok(result);
}).WithTags("Rebalancing");

app.Run();

// Request body records (separate from command records so binding works cleanly).
record CreatePortfolioRequestBody(string Name, decimal DriftTolerancePct, IReadOnlyList<AllocationInput> Allocations);
record AddHoldingRequestBody(string Ticker, IReadOnlyList<LotInput> Lots);
record PricesBody(IReadOnlyDictionary<string, decimal> Prices);

// Expose for integration tests.
public partial class Program { }
