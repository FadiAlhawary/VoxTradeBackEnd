using Microsoft.AspNetCore.Mvc;
using VoxTrade.Services.Interface;

namespace VoxTrade.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HistoryController : ControllerBase
    {
        private readonly IHistoryRepository _historyRepository;
        private readonly ILogger<HistoryController> _logger;

        public HistoryController(IHistoryRepository historyRepository, ILogger<HistoryController> logger)
        {
            _historyRepository = historyRepository;
            _logger = logger;
        }

        [HttpGet("GetOrderHistory")]
        public async Task<IActionResult> GetOrderHistory(int userId, [FromQuery] bool activeOnly = false)
        {
            try
            {
                var result = await _historyRepository.GetOrderHistory(userId, activeOnly);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get order history for user {UserId}", userId);
                return StatusCode(500, "An error occurred while retrieving order history.");
            }
        }
        [HttpGet("GetWalletHistoryWithDate")]
        public async Task<IActionResult> GetWalletHistoryWithDate(
    int userId,
    DateTime? from = null,
    DateTime? to = null)
        {
            try
            {
                var result = await _historyRepository.GetWalletHistoryWithDate(userId, from,to);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get order history for user {UserId}", userId);
                return StatusCode(500, "An error occurred while retrieving order history.");
            }
        }

        [HttpPut("CancelOrder")]
        public async Task<IActionResult> CancelOrder(int orderId, [FromQuery] int userId)
        {
            try
            {
                var cancelled = await _historyRepository.CancelPendingOrder(userId, orderId);

                if (!cancelled)
                    return BadRequest("Order cannot be cancelled. It may already be executed, partially filled, cancelled, or not belong to this user.");

                return Ok(new
                {
                    success = true,
                    message = "Order cancelled successfully."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel order {OrderId} for user {UserId}", orderId, userId);
                return StatusCode(500, "An error occurred while cancelling the order.");
            }
        }

        [HttpGet("GetTradeHistory")]
        public async Task<IActionResult> GetTradeHistory(int userId)
        {
            try
            {
                var result = await _historyRepository.GetTradeHistory(userId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get trade history for user {UserId}", userId);
                return StatusCode(500, "An error occurred while retrieving trade history.");
            }
        }

        [HttpGet("GetWalletHistory")]
        public async Task<IActionResult> GetWalletHistory(int userId)
        {
            try
            {
                var result = await _historyRepository.GetWalletHistory(userId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get wallet history for user {UserId}", userId);
                return StatusCode(500, "An error occurred while retrieving wallet history.");
            }
        }

        [HttpGet("GetOrderStatuses")]
        public async Task<IActionResult> GetOrderStatuses()
        {
            var result = await _historyRepository.GetOrderStatuses();
            return Ok(result);
        }

        [HttpGet("GetOrderTypes")]
        public async Task<IActionResult> GetOrderTypes()
        {
            var result = await _historyRepository.GetOrderTypes();
            return Ok(result);
        }

        [HttpGet("GetOrderActions")]
        public async Task<IActionResult> GetOrderActions()
        {
            var result = await _historyRepository.GetOrderActions();
            return Ok(result);
        }
    }
}
