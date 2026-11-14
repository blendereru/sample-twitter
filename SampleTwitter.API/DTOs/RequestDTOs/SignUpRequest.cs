using System.ComponentModel.DataAnnotations;
using SampleTwitter.API.DTOs.Validation;

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

    /// <summary>
    /// Optional public handle (3–30 letters, digits or underscores).
    /// When omitted, a random username such as <c>user_k3f9x2ab</c> is assigned and can be changed later.
    /// </summary>
    [Username]
    public string? Username { get; set; }
}
