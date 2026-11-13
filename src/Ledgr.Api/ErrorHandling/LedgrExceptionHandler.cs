using Ledgr.Api.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Ledgr.Api.Middleware;

public sealed class LedgrExceptionHandler(
    ILogger<LedgrExceptionHandler> logger,
    IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, detail) = Describe(exception);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                exception,
                "Unhandled exception for {Method} {Path}",
                httpContext.Request.Method,
                httpContext.Request.Path);
        }
        else
        {
            logger.LogWarning(
                "Rejected {Method} {Path}: {Status} {Reason}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                statusCode,
                exception.Message);
        }

        httpContext.Response.StatusCode = statusCode;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Instance = httpContext.Request.Path,
                Type = $"https://httpstatuses.io/{statusCode}",
                Extensions = { ["traceId"] = httpContext.TraceIdentifier }
            }
        });
    }

    private static (int StatusCode, string Title, string Detail) Describe(Exception exception) => exception switch
    {
        LedgrException known => (known.StatusCode, known.Title, known.Message),
        _ => (
            StatusCodes.Status500InternalServerError,
            "Unexpected error",
            "The request could not be completed. Retry, and check the server logs if it keeps failing.")
    };
}
