namespace iko_host.Infrastructure;

using iko_host.Exceptions;
using Microsoft.AspNetCore.Diagnostics;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, message) = exception switch
        {
            UnsupportedPlatformException => (StatusCodes.Status400BadRequest, exception.Message),
            PlatformApiException pae => (StatusCodes.Status502BadGateway,
                $"{pae.Platform} API error: {pae.Message}"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        _logger.LogError(exception, "Unhandled exception on {Method} {Path}",
            httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(
            new { data = (object?)null, error = message }, cancellationToken);
        return true;
    }
}
