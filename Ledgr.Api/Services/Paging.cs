namespace Ledgr.Api.Services;

internal static class Paging
{
    public const int MaxPageSize = 100;

    public static (int Page, int PageSize) Normalize(int page, int pageSize)
        => (Math.Max(page, 1), Math.Clamp(pageSize, 1, MaxPageSize));
}
