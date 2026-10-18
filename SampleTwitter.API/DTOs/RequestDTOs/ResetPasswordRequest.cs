using System.ComponentModel.DataAnnotations;

namespace SampleTwitter.API.DTOs.RequestDTOs;

/// <summary>
/// Request payload for resetting a password using a reset token.
/// </summary>
public class ResetPasswordRequest
{
    [Required]
    public long UserId { get; set; }

    [Required]
    public string Token { get; set; } = null!;

    [Required]
    [MinLength(8)]
    public string NewPassword { get; set; } = null!;
}
