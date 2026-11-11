namespace Ledgr.Api.Middleware;

/// <summary>
/// Adds the response headers a browser needs to stay out of trouble. The CSP is skipped for
/// Swagger UI, which relies on inline script and would otherwise be blocked.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string ContentSecurityPolicy =
        "default-src 'self'; img-src 'self' data:; style-src 'self'; script-src 'self'; " +
        "base-uri 'none'; form-action 'self'; frame-ancestors 'none'";

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["X-Frame-Options"] = "DENY";

        if (!context.Request.Path.StartsWithSegments("/swagger"))
        {
            context.Response.Headers["Content-Security-Policy"] = ContentSecurityPolicy;
        }

        await next(context);
    }
}
