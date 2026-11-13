namespace Ledgr.Api.Exceptions;

/// <summary>
/// A request that was understood but cannot be carried out. Carries the HTTP status the
/// API should return; anything not deriving from this type is treated as a server fault.
/// </summary>
public class LedgrException : Exception
{
    public LedgrException(string message, int statusCode = 400, string title = "Request rejected")
        : base(message)
    {
        StatusCode = statusCode;
        Title = title;
    }

    public int StatusCode { get; }

    public string Title { get; }
}

