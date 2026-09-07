using System.ComponentModel.DataAnnotations;

namespace SampleTwitter.API.DTOs.RequestDTOs;

/// <summary>
/// Credentials for registering a new account.
/// </summary>
public class SignUpRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = null!;
}