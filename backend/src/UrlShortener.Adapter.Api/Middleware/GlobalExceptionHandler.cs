using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using UrlShortener.Application.Abstract.Primary.Exceptions;
using ILogger = Serilog.ILogger;

namespace UrlShortener.Adapter.Api.Middleware;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger _logger;

    public GlobalExceptionHandler(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        switch (exception)
        {
            case ValidationException validationEx:
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                await httpContext.Response.WriteAsJsonAsync(
                    new
                    {
                        errors = validationEx.Errors
                            .Select(e => new { field = e.PropertyName, message = e.ErrorMessage })
                    },
                    cancellationToken);
                return true;

            case AliasConflictException conflictEx:
                httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
                await httpContext.Response.WriteAsJsonAsync(
                    new
                    {
                        message = conflictEx.Message,
                        conflictingAlias = conflictEx.ConflictingAlias,
                        suggestions = conflictEx.Suggestions
                    },
                    cancellationToken);
                return true;

            default:
                _logger.Error(exception, "Unhandled exception");
                httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await httpContext.Response.WriteAsJsonAsync(
                    new { message = "An unexpected error occurred." },
                    cancellationToken);
                return true;
        }
    }
}
