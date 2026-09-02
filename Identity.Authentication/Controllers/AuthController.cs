using Identity.Authentication.Contracts;
using Identity.Authentication.Endpoints;
using Identity.Authentication.Entities;
using Identity.Authentication.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Identity.Authentication.Controllers
{
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ITokenService _tokenService;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ITokenService tokenService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _tokenService = tokenService;
        }

        [HttpPost(AuthEndpoints.Auth.Register)]
        [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken = default)
        {
            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email
            };

            var result = await _userManager.CreateAsync(user, request.Password);

            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }

            return StatusCode(StatusCodes.Status201Created, new RegisterResponse(user.Id, user.Email!));
        }

        [HttpPost(AuthEndpoints.Auth.Login)]
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user is null)
            {
                return Unauthorized();
            }

            var signInResult = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            if (!signInResult.Succeeded)
            {
                return Unauthorized();
            }

            var roles = await _userManager.GetRolesAsync(user);
            var token = await _tokenService.GenerateTokenAsync(user, roles, cancellationToken);

            return Ok(new LoginResponse(token.AccessToken, token.ExpiresAtUtc));
        }

        [HttpGet(AuthEndpoints.Auth.Users)]
        [Authorize(Roles = Roles.Admin)]
        [ProducesResponseType(typeof(GetUsersResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUsers([FromQuery] int pageSize = 10, [FromQuery] int pageIndex = 0, CancellationToken cancellationToken = default)
        {
            var query = _userManager.Users.OrderBy(u => u.Email);

            var totalCount = await query.CountAsync(cancellationToken);

            var users = await query
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .Select(u => new UserResponse(u.Id, u.Email!))
                .ToListAsync(cancellationToken);

            return Ok(new GetUsersResponse(users, totalCount));
        }
    }
}
