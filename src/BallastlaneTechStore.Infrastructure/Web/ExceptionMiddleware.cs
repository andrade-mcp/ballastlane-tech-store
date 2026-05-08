using BallastlaneTechStore.Application.Common;
using BallastlaneTechStore.Domain.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BallastlaneTechStore.Infrastructure.Web;

// Translates app/domain exceptions into ProblemDetails so controllers stay clean.
public sealed class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _log;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> log) { _next = next; _log = log; }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try { await _next(ctx); }
        catch (NotFoundException ex)        { await Write(ctx, StatusCodes.Status404NotFound,         "Not found",            ex.Message); }
        catch (ConflictException ex)        { await Write(ctx, StatusCodes.Status409Conflict,         "Conflict",             ex.Message); }
        catch (OutOfStockException ex)      { await Write(ctx, StatusCodes.Status409Conflict,         "Out of stock",         ex.Message); }
        catch (ValidationException ex)      { await Write(ctx, StatusCodes.Status400BadRequest,       "Validation failed",    ex.Message); }
        catch (DomainException ex)          { await Write(ctx, StatusCodes.Status400BadRequest,       "Validation failed",    ex.Message); }
        catch (AuthenticationException ex)  { await Write(ctx, StatusCodes.Status401Unauthorized,     "Authentication failed", ex.Message); }
        catch (Exception ex)
        {
            _log.LogError(ex, "Unhandled exception");
            await Write(ctx, StatusCodes.Status500InternalServerError, "Server error", "Unexpected error.");
        }
    }

    private static Task Write(HttpContext ctx, int status, string title, string detail)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/problem+json";
        return ctx.Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Title = title, Detail = detail });
    }
}
