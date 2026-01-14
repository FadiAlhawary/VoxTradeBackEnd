using Microsoft.AspNetCore.Mvc;
using VoxTrade.Models;
using VoxTrade.Services.Interface;

namespace VoxTrade.Controllers
{
    [Route("api/[controller]")]
    [Produces("application/json")]
    //[Microsoft.AspNetCore.Authorization.Authorize]
    [ApiController]
    public class UIThemesController:ControllerBase
    {
        private readonly ILogger<RolesController> _logger;
        private readonly IUIThemesRepository _IUIThemesRepository;

        public UIThemesController(ILogger<RolesController> logger, IUIThemesRepository IUIThemesRepository)
        {
            _IUIThemesRepository = IUIThemesRepository;
            _logger = logger;
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
    }
}
