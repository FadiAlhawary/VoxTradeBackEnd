using Microsoft.AspNetCore.Mvc;
using VoxTrade.Models.DTO;
using VoxTrade.Services.Interface;

namespace VoxTrade.Controllers
{
    [Route("api/[controller]")]
    [Produces("application/json")]
    //[Microsoft.AspNetCore.Authorization.Authorize]
    [ApiController]
    public class PortfolioController : ControllerBase
    {
        private readonly IPortfolioRepository _IPortfolioRepository;

        public PortfolioController(IPortfolioRepository IPortfolioRepository)
        {
            _IPortfolioRepository = IPortfolioRepository;
        }
        [HttpGet("GetPortfolio")]
        public async Task<IActionResult> GetPortfolio(int userId)
        {
            return Ok(
                await _IPortfolioRepository.GetPortfolio(userId)
                );
        }

        [HttpGet("GetHoldingSummary")]
        public async Task<IActionResult> GetHoldingSummary(int userId, string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return BadRequest("symbol is required");

            return Ok(await _IPortfolioRepository.GetHoldingSummary(userId, symbol.Trim()));
        }

        [HttpGet("GetProfitLossChart")]
        public async Task<IActionResult> GetProfitLossChart(
      int userId,
      [FromQuery] DateTime? from,
      [FromQuery] DateTime? to)
        {
            var result = await _IPortfolioRepository.GetProfitLossChart(userId, from, to);
            return Ok(result);
        }
    }
}
