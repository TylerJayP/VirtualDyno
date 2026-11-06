using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using VirtualDyno.Engine.Analysis;
using VirtualDyno.Engine.Logs;

namespace VirtualDyno.API.Services;

/// <summary>
/// Turns expected failures into ProblemDetails with a message the user can act on:
/// 400 for a bad request, 422 for a log the dyno can't use. Anything else is a 500 that logs the
/// exception but never sends internals to the client.
/// </summary>
public sealed class DynoExceptionHandler(IProblemDetailsService problemDetails, ILogger<DynoExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        (int status, string title, string? detail) = exception switch
        {
            InvalidUploadException e => (StatusCodes.Status400BadRequest, "Invalid request", e.Message),
            LogFormatException e => (StatusCodes.Status422UnprocessableEntity, "Can't read this log", e.Message),
            DynoAnalysisException e => (StatusCodes.Status422UnprocessableEntity, "Can't build a dyno curve from this log", e.Message),
            _ => (StatusCodes.Status500InternalServerError, "Something went wrong", null),
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled error on {Method} {Path}", context.Request.Method, context.Request.Path);

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail },
        });
    }
}
