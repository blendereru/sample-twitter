using System.ComponentModel.DataAnnotations;

namespace SampleTwitter.API.DTOs.RequestDTOs;

/// <summary>
/// Request to edit an existing post. Must retain at least Text or an ImageUrl.
/// </summary>
public class EditPostRequest
{
    [MaxLength(280)]
    public string? Text { get; set; }

    [Url]
    public string? ImageUrl { get; set; }
}