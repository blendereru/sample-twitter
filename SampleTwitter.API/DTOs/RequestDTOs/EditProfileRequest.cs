using System.ComponentModel.DataAnnotations;

namespace SampleTwitter.API.DTOs.RequestDTOs;

/// <summary>
/// Request to edit a user's profile. Must retain at least Username or ImageUrl.
/// </summary>
public class EditProfileRequest
{
    [MaxLength(50)]
    public string? Username { get; set; }

    [Url]
    public string? ImageUrl { get; set; }
}