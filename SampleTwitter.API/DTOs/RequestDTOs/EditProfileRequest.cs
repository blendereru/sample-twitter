using System.ComponentModel.DataAnnotations;
using SampleTwitter.API.DTOs.Validation;

namespace SampleTwitter.API.DTOs.RequestDTOs;

/// <summary>
/// Request to edit a user's profile. The username is always required; the image is optional.
/// </summary>
public class EditProfileRequest
{
    [Required]
    [Username]
    public string Username { get; set; } = null!;

    [Url]
    public string? ImageUrl { get; set; }
}