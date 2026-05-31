using Microsoft.AspNetCore.Mvc;
using VoxTrade.Dashboard.Interfaces;

namespace VoxTrade.Dashboard.Controllers;

[ApiController]
[Route("api/dashboard")]
public class UserDashboardController : ControllerBase
{
    private readonly IUserDashboardService _dashboardService;

    public UserDashboardController(IUserDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>Home screen: wallet, orders, positions, trades, recent activity.</summary>
    [HttpGet("{userId:int}")]
    public async Task<IActionResult> GetSummary(int userId, [FromQuery] int recentLimit = 10)
    {
        var result = await _dashboardService.GetSummaryAsync(userId, recentLimit);
        if (result == null)
            return NotFound();
        return Ok(result);
    }

    [HttpGet("{userId:int}/wallet")]
    public async Task<IActionResult> GetWallet(int userId)
    {
        var result = await _dashboardService.GetWalletSummaryAsync(userId);
        if (result == null)
            return NotFound();
        return Ok(result);
    }

    [HttpGet("{userId:int}/orders/summary")]
    public async Task<IActionResult> GetOrdersSummary(int userId)
    {
        var result = await _dashboardService.GetOrdersSummaryAsync(userId);
        return Ok(result);
    }

    [HttpGet("{userId:int}/positions")]
    public async Task<IActionResult> GetPositions(int userId)
    {
        var result = await _dashboardService.GetPositionsAsync(userId);
        return Ok(result);
    }

    [HttpGet("{userId:int}/recent-activity")]
    public async Task<IActionResult> GetRecentActivity(int userId, [FromQuery] int limit = 20)
    {
        var result = await _dashboardService.GetRecentActivityAsync(userId, limit);
        return Ok(result);
    }

    /// <summary>Active instruments with live quotes (market tab).</summary>
    [HttpGet("market-snapshot")]
    public async Task<IActionResult> GetMarketSnapshot([FromQuery] int limit = 20)
    {
        var result = await _dashboardService.GetMarketSnapshotAsync(limit);
        return Ok(result);
    }

    /// <summary>Search users to send money to (excludes self when excludeUserId set).</summary>
    [HttpGet("transfer-recipients/search")]
    public async Task<IActionResult> SearchTransferRecipients(
        [FromQuery] string query,
        [FromQuery] int limit = 20,
        [FromQuery] int? excludeUserId = null)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Ok(Array.Empty<object>());

        var result = await _dashboardService.SearchTransferRecipientsAsync(query, limit, excludeUserId);
        return Ok(result);
    }
}
