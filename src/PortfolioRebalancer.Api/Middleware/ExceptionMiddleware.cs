using System.Net;
using System.Text.Json;
using PortfolioRebalancer.Api.Domain;

namespace PortfolioRebalancer.Api.Middleware;

public sealed class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (DomainException ex)
        {
            logger.LogWarning("Domain validation failure: {Message}", ex.Message);
            await WriteError(ctx, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            await WriteError(ctx, HttpStatusCode.InternalServerError, "An unexpected error occurred.");
        }
    }

    private static async Task WriteError(HttpContext ctx, HttpStatusCode status, string message)
    {
        ctx.Response.StatusCode = (int)status;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(new { error = message }, SerializerOptions));
    }
}
