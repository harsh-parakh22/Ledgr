using Ledgr.Api.Contracts;
using Ledgr.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ledgr.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController(IDashboardService dashboard) : ControllerBase
{
    [HttpGet("kpis")]
    [ProducesResponseType<DashboardKpisDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardKpisDto>> GetKpis(CancellationToken cancellationToken)
    {
        return Ok(await dashboard.GetAsync(cancellationToken));
    }
}
