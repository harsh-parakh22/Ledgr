using Ledgr.Api.Contracts;

namespace Ledgr.Api.Services;

public interface IDashboardService
{
    Task<DashboardKpisDto> GetAsync(CancellationToken cancellationToken);
}
