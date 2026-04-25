using Microsoft.AspNetCore.Mvc;
using VoxTrade.Models.DTO;
using VoxTrade.Services.Interface;

namespace VoxTrade.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MarketController :ControllerBase
    {
        private readonly IMarketRepository _IMarketRepository;
        public MarketController (IMarketRepository IMarketRepository)
        {
            _IMarketRepository = IMarketRepository;
        }


        [HttpPost("PlaceOrder")]
        public async Task<IActionResult> PlaceOrder([FromBody] PlaceOrderRequestDto request)
        {
            PlaceOrderResponseDto placeOrderResponseDto = new PlaceOrderResponseDto();
            placeOrderResponseDto = await _IMarketRepository.PlaceOrder(request);

            if (placeOrderResponseDto is null)
                return StatusCode(500, "Invalid database response");

            if (!placeOrderResponseDto.Success)
                return BadRequest(placeOrderResponseDto);

            return Ok(placeOrderResponseDto);
        }

    }
}
