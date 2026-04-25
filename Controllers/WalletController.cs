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
        public WalletController(IWalletRepo walletRepo)
        {
            _walletRepo = walletRepo;
        }

        [HttpGet("GetWallet")]
       public async Task<IActionResult> GetWallet(int userId, bool WithHisory = false)
        {
             return Ok(await _walletRepo.GetWallet(userId, WithHisory));
        }


    }
}
