using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using SampleTwitter.API.Abstractions;
using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.DTOs.ResponseDTOs;

namespace SampleTwitter.API.Controllers;
[Route("api/account")]
[ApiController]
public class AccountController : ControllerBase
{
    private readonly IAccountService _accountService;
    private readonly IEmailConfirmationService _emailConfirmationService;
    private readonly ILogger<AccountController> _logger;
    public AccountController(IAccountService accountService, IEmailConfirmationService emailConfirmationService, 
        ILogger<AccountController> logger)
    {
        _accountService = accountService;
        _emailConfirmationService = emailConfirmationService;
        _logger = logger;
    }
    
    /// <summary>
    /// Registers a new user account and sends a confirmation email.
    /// </summary>
    /// <response code="201">A new user was created; check the response body for the user ID and confirmation instructions.</response>
    /// <response code="200">An existing unconfirmed registration was found; the password was updated and the confirmation email resent.</response>
    /// <response code="400">The request body failed validation (e.g. missing or malformed email, password too short).</response>
    /// <response code="409">A confirmed account already exists for this email address.</response>
    /// <response code="500">An unexpected error occurred while processing the request.</response>
    [HttpPost("signup")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(SignUpResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(SignUpResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SignUp(SignUpRequest request)
    {
        var result = await _accountService.Register(request);

        var payload = new SignUpResponse(
            result.UserId,
            "Registration successful. Please check your email to confirm your account.");

        return result.IsNewRegistration
            ? CreatedAtAction(nameof(GetUser), new { id = result.UserId }, payload)
            : Ok(payload);
    }

    /// <summary>
    /// Confirms a user's email address using the token sent via the confirmation email.
    /// </summary>
    /// <response code="200">The email address was confirmed successfully.</response>
    /// <response code="400">The confirmation link is invalid, expired, or already used.</response>
    /// <response code="500">An unexpected error occurred while processing the request.</response>
    [HttpPost("confirm-email")]
    [ProducesResponseType(typeof(ConfirmEmailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ConfirmEmail([FromQuery] long userId, [FromQuery] string token, CancellationToken ct)
    {
        var user = await _emailConfirmationService.ConfirmEmail(userId, token, ct);
        
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email)
        };
        
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true, 
                IssuedUtc = DateTimeOffset.UtcNow
            });
        
        _logger.LogInformation("User {UserId} confirmed email and signed in", user.Id);
        return Ok(new ConfirmEmailResponse("Your email has been confirmed. You are now signed in."));
    }
    
    [HttpGet("{id}")]
    public async Task<IActionResult> GetUser(long id)
    {
        throw new NotImplementedException();
    }
}