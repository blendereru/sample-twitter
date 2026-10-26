using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SampleTwitter.API.Abstractions;
using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.DTOs.ResponseDTOs;

namespace SampleTwitter.API.Controllers;
[Tags("Account")]
[Route("api/auth")]
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
    public async Task<IActionResult> SignUp(SignUpRequest request, CancellationToken ct)
    {
        var result = await _accountService.Register(request, ct);

        var payload = new SignUpResponse(
            result.UserId,
            "Registration successful. Please check your email to confirm your account.");

        return result.IsNewRegistration
            ? CreatedAtAction(actionName: "GetUser", controllerName: "Users",  new { id = result.UserId }, payload)
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
        var result = await _emailConfirmationService.ConfirmEmail(userId, token, ct);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.UserId.ToString()),
            new(ClaimTypes.Email, result.Email)
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

        _logger.LogInformation("User {UserId} confirmed email and signed in", result.UserId);
        return Ok(new ConfirmEmailResponse("Your email has been confirmed. You are now signed in."));
    }

    /// <summary>
    /// Resends a confirmation email if an unconfirmed account exists for the specified email address.
    /// Always returns 200 OK to prevent email enumeration.
    /// </summary>
    /// <response code="200">If an unconfirmed account with that email exists, a confirmation link has been sent.</response>
    /// <response code="400">The request body failed validation (e.g. malformed email).</response>
    /// <response code="500">An unexpected error occurred while processing the request.</response>
    [HttpPost("resend-confirmation-email")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ResendConfirmationEmailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ResendConfirmationEmail(ResendConfirmationEmailRequest request, CancellationToken ct)
    {
        await _emailConfirmationService.ResendConfirmationEmail(request, ct);

        return Ok(new ResendConfirmationEmailResponse(
            "If an unconfirmed account with that email exists, a new confirmation link has been sent."));
    }

    /// <summary>
    /// Authenticates a user with email and password and issues an authentication cookie.
    /// </summary>
    /// <response code="200">The user was authenticated successfully.</response>
    /// <response code="400">The request body failed validation (e.g. missing or malformed email).</response>
    /// <response code="401">The email or password is incorrect.</response>
    /// <response code="403">The user's email address has not been confirmed yet.</response>
    /// <response code="500">An unexpected error occurred while processing the request.</response>
    [HttpPost("signin")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await _accountService.Login(request, ct);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.UserId.ToString()),
            new(ClaimTypes.Email, result.Email)
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

        _logger.LogInformation("User {UserId} logged in", result.UserId);

        return Ok(new LoginResponse(result.UserId, result.Email, "Login successful."));
    }

    /// <summary>
    /// Signs out the currently authenticated user by clearing the authentication cookie.
    /// </summary>
    /// <response code="200">The user was signed out successfully.</response>
    /// <response code="401">The request is not authenticated.</response>
    /// <response code="500">An unexpected error occurred while processing the request.</response>
    [Authorize]
    [HttpPost("signout")]
    [ProducesResponseType(typeof(SignOutResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SignOutAccount()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        _logger.LogInformation("User {UserId} signed out", userIdClaim);

        return Ok(new SignOutResponse("Signed out successfully."));
    }

    /// <summary>
    /// Returns the profile information of the currently authenticated user.
    /// </summary>
    /// <response code="200">The authenticated user's profile information.</response>
    /// <response code="401">The request is not authenticated (no valid auth cookie).</response>
    /// <response code="404">The authenticated user's account could not be found.</response>
    /// <response code="500">An unexpected error occurred while processing the request.</response>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(MeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userId = long.Parse(userIdClaim!);

        var result = await _accountService.GetUserById(userId, ct);

        return Ok(new MeResponse(result.Id, result.Email, result.RegisteredAt, result.Username, result.ImageUrl, result.UpdatedAt));
    }

    /// <summary>
    /// Changes the password of the currently authenticated user after verifying the current password.
    /// </summary>
    /// <response code="200">The password was updated successfully.</response>
    /// <response code="400">The request body failed validation (e.g. new password too short).</response>
    /// <response code="401">The request is not authenticated or the current password is incorrect.</response>
    /// <response code="404">The authenticated user's account could not be found.</response>
    /// <response code="500">An unexpected error occurred while processing the request.</response>
    [Authorize]
    [HttpPut("password")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ChangePasswordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userId = long.Parse(userIdClaim!);

        await _accountService.ChangePassword(userId, request, ct);

        return Ok(new ChangePasswordResponse("Password updated successfully."));
    }

    /// <summary>
    /// Updates the email address of the currently authenticated user after verifying the current password,
    /// marks the email as unconfirmed, signs out the current session, and sends a confirmation email to the new address.
    /// </summary>
    /// <response code="200">The email address was updated; check the new email inbox for the confirmation link.</response>
    /// <response code="400">The request body failed validation (e.g. malformed email).</response>
    /// <response code="401">The request is not authenticated or the current password is incorrect.</response>
    /// <response code="404">The authenticated user's account could not be found.</response>
    /// <response code="409">Another account already uses the requested email address.</response>
    /// <response code="500">An unexpected error occurred while processing the request.</response>
    [Authorize]
    [HttpPut("email")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ChangeEmailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ChangeEmail(ChangeEmailRequest request, CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userId = long.Parse(userIdClaim!);

        var result = await _accountService.ChangeEmail(userId, request, ct);

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        return Ok(new ChangeEmailResponse(
            result.UserId,
            result.Email,
            "Email updated. Please check your new email inbox to confirm your address."));
    }

    /// <summary>
    /// Initiates a password reset by sending a reset link to the specified email address if a confirmed account exists.
    /// Always returns 200 OK to prevent email enumeration.
    /// </summary>
    /// <response code="200">If a confirmed account with that email exists, a password-reset-link has been sent.</response>
    /// <response code="400">The request body failed validation (e.g. malformed email).</response>
    /// <response code="500">An unexpected error occurred while processing the request.</response>
    [HttpPost("forgot-password")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ForgotPasswordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        await _accountService.ForgotPassword(request, ct);

        return Ok(new ForgotPasswordResponse(
            "If a confirmed account with that email exists, a password reset link has been sent."));
    }

    /// <summary>
    /// Resets a user's password using a valid password reset token.
    /// </summary>
    /// <response code="200">The password has been reset successfully.</response>
    /// <response code="400">The request failed validation, or the reset token is invalid, expired, or already used.</response>
    /// <response code="500">An unexpected error occurred while processing the request.</response>
    [HttpPost("reset-password")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ResetPasswordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        await _accountService.ResetPassword(request, ct);

        return Ok(new ResetPasswordResponse("Your password has been reset successfully. You can now sign in."));
    }
}
