using System.ComponentModel.DataAnnotations;

namespace SampleTwitter.API.DTOs.RequestDTOs;

/// <summary>
/// Request payload for resending an account confirmation email.
/// </summary>
public class ResendConfirmationEmailRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;
}