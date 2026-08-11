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
    public AccountController(IAccountService accountService)
    {
        _accountService = accountService;
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

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUser(long id)
    {
        throw new NotImplementedException();
    }
}