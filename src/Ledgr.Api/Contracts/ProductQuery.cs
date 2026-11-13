namespace Ledgr.Api.Contracts;

public sealed record ProductQuery(string? Search, string? Category, bool OnlyLowStock, int Page, int PageSize);

