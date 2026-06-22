using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Pinowo.Models;

namespace Pinowo.Controllers.Api
{
    /// <summary>
    /// REST API for users (PRD Section 6):
    ///   POST /api/users/register
    ///   POST /api/users/login
    /// Login uses cookie auth via SignInManager, so a curl/Postman client can
    /// log in once (saving the cookie) and reuse it against the other endpoints.
    /// </summary>
    [ApiController]
    [Route("api/users")]
    [Produces("application/json")]
    public class UsersApiController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;

        public UsersApiController(UserManager<User> userManager, SignInManager<User> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            var user = new User
            {
                Name = req.Name,
                UserName = req.Email,
                Email = req.Email,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, req.Password);
            if (!result.Succeeded)
                return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

            var dto = new UserDto(user.Id, user.Name, user.Email!);
            return CreatedAtAction(nameof(Register), new { id = user.Id }, dto);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            var result = await _signInManager.PasswordSignInAsync(
                req.Email, req.Password, isPersistent: false, lockoutOnFailure: false);

            if (!result.Succeeded)
                return Unauthorized(new { error = "Invalid email or password." });

            var user = await _userManager.FindByEmailAsync(req.Email);
            return Ok(new UserDto(user!.Id, user.Name, user.Email!));
        }
    }
}
