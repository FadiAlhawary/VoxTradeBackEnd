using Microsoft.AspNetCore.Mvc;
using VoxTrade.Services.Interface;

namespace VoxTrade.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InstrumentController : ControllerBase
    {
        private readonly IInstrumentRepository _instrumentRepository;
        private readonly ILogger<InstrumentController> _logger;

        public InstrumentController(
            IInstrumentRepository instrumentRepository,
            ILogger<InstrumentController> logger)
        {
            _instrumentRepository = instrumentRepository;
            _logger = logger;
        }

        [HttpGet("GetAllInstrument")]
        public async Task<IActionResult> GetAllInstrument(bool activeOnly = false)
        {
            try
            {
                var result = await _instrumentRepository.GetAllInstruments(activeOnly);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get all instruments");
                return StatusCode(500, "An error occurred while retrieving instruments.");
            }
        }

        [HttpGet("GetInstrumentTypes")]
        public async Task<IActionResult> GetInstrumentTypes()
        {
            try
            {
                var result = await _instrumentRepository.GetInstrumentTypes();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get instrument types");
                return StatusCode(500, "An error occurred while retrieving instrument types.");
            }
        }

        [HttpGet("GetInstrumentStatuses")]
        public async Task<IActionResult> GetInstrumentStatuses()
        {
            try
            {
                var result = await _instrumentRepository.GetInstrumentStatuses();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get instrument statuses");
                return StatusCode(500, "An error occurred while retrieving instrument statuses.");
            }
        }
    }
}
