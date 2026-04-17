using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VoxTrade.Api.Data;
using VoxTrade.Models;
using VoxTrade.Services.Interface;

namespace VoxTrade.Controllers
{
    [Route("api/[controller]")]
    [Produces("application/json")]
    //GetAllLookUps[Microsoft.AspNetCore.Authorization.Authorize]
    [ApiController]
    public class UIThemesController:ControllerBase
    {
        private readonly ILogger<RolesController> _logger;
        private readonly IUIThemesRepository _IUIThemesRepository;
        private readonly TradingDbContext _context;


        public UIThemesController(ILogger<RolesController> logger, IUIThemesRepository IUIThemesRepository, TradingDbContext TradingDbContext)
        {
            _IUIThemesRepository = IUIThemesRepository;
            _logger = logger;
            _context = TradingDbContext;
        }
        [HttpGet]
        [Route("GetUIThemeByPurposeId")]
        public async Task<IActionResult> GetUIThemeByPurposeId(int id)
        {
            try
            {
                List<UIThemes> roles = new List<UIThemes>();
                roles = await _IUIThemesRepository.GetUIThemeByPurposeId(id);
                return Ok(roles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get roles");
                return StatusCode(500, "Internal server error");

            }
        }
        [HttpGet]
        [Route("GetLookUp")]
        public async Task<IActionResult> GetLookUp()
        {
            try
            {
                List<Lookup> lookups= new List<Lookup>();
                lookups = await _context.LookUp.ToListAsync();
                return Ok(lookups);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get roles");
                return StatusCode(500, "Internal server error");

            }
        }
    }
}
