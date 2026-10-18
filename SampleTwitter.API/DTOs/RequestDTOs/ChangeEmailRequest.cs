using System.ComponentModel.DataAnnotations;

namespace SampleTwitter.API.DTOs.RequestDTOs;

/// <summary>
/// Request payload for changing an authenticated user's email address.
/// </summary>
public class ChangeEmailRequest
{
    [Required]
    [EmailAddress]
    public string NewEmail { get; set; } = null!;

    [Required]
    public string CurrentPassword { get; set; } = null!;
}
