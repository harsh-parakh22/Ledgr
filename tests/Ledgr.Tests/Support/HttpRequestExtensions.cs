namespace Ledgr.Tests.Support;

internal static class HttpRequestExtensions
{
    public static HttpRequestMessage WithHeader(this HttpRequestMessage request, string name, string value)
    {
        request.Headers.Add(name, value);
        return request;
    }
}

