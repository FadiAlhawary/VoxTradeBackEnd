using Microsoft.AspNetCore.Mvc;
using VoxTrade.Models.DTO;
using VoxTrade.Services.Interface;

namespace VoxTrade.Controllers
{
    [Route("api/[controller]")]
    [Produces("application/json")]
    //[Microsoft.AspNetCore.Authorization.Authorize]
    [ApiController]
    public class WalletController : ControllerBase
    {
        private readonly IWalletRepo _walletRepo;
        private readonly IWalletTransferService _walletTransferService;

        public WalletController(IWalletRepo walletRepo, IWalletTransferService walletTransferService)
        {
            _walletRepo = walletRepo;
            _walletTransferService = walletTransferService;
        }

        [HttpGet("GetWallet")]
        public async Task<IActionResult> GetWallet(int userId, bool WithHisory = false)
        {
            return Ok(await _walletRepo.GetWallet(userId, WithHisory));
        }

        /// <summary>
        /// Transfer funds from one user wallet to another (balance and available_balance).
        /// </summary>
        [HttpPost("Transfer")]
        public async Task<IActionResult> TransferMoney([FromBody] TransferMoneyRequestDto request)
        {
            var result = await _walletTransferService.TransferMoneyAsync(request);

            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }
    }
}
