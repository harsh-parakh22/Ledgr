using Ledgr.Api.Dtos;

namespace Ledgr.Api.Services;

public interface IDashboardService
{
    Task<DashboardKpisDto> GetAsync(CancellationToken cancellationToken);
}
