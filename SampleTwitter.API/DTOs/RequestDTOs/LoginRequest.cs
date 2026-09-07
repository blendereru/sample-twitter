using System.ComponentModel.DataAnnotations;

namespace SampleTwitter.API.DTOs.RequestDTOs;

/// <summary>
/// Credentials for account sign-in.
/// </summary>
public class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    public string Password { get; set; } = null!;
}
