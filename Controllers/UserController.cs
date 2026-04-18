using Microsoft.AspNetCore.Mvc;
using VoxTrade.Models.DTO;

namespace VoxTrade.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        public UserController(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }
        [HttpGet("GetUserProfileById")]
        public async Task<IActionResult> GetUserProfileById(int userId)
        {
            var user = await _userRepository.GetUserProfileById(userId);

            if (user == null)
                return NotFound($"User with id {userId} not found");

            return Ok(user);
        }

    }
}
