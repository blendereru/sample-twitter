using System.ComponentModel.DataAnnotations;
using SampleTwitter.API.Models;

namespace SampleTwitter.API.DTOs.RequestDTOs;

/// <summary>
/// Request to edit a user's profile. Must retain at least Username or ImageUrl.
/// </summary>
public class EditProfileRequest
{
    [MaxLength(User.MaxUsernameLength)]
    public string? Username { get; set; }

    [Url]
    public string? ImageUrl { get; set; }
}