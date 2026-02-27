using Microsoft.AspNetCore.Mvc;
using VoxTrade.Models;
using VoxTrade.Services.Interface;

namespace VoxTrade.Controllers
{
    [Route("api/[controller]")]
    [Produces("application/json")]
    //[Microsoft.AspNetCore.Authorization.Authorize]
    [ApiController]
    public class RolesController : ControllerBase
    {
        private readonly ILogger<RolesController> _logger;
        private readonly IRolesRepository _IRolesRepository;

        public RolesController(ILogger<RolesController> logger, IRolesRepository IRolesRepository)
        {
             _IRolesRepository = IRolesRepository;
            _logger = logger;
        }
        [HttpGet]
        [Route("GetAllRoles")]
        public async Task<IActionResult> GetAllRoles()
        {
            try
            {
                List<Roles> roles = new List<Roles>();
                roles = await _IRolesRepository.GetAllRoles();
                return Ok(roles);
            }catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get roles"); 
                return StatusCode(500, "Internal server error");

            }
        }
    }
}
