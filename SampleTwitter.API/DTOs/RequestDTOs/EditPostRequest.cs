using System.ComponentModel.DataAnnotations;
using SampleTwitter.API.Models;

namespace SampleTwitter.API.DTOs.RequestDTOs;

/// <summary>
/// Request to edit an existing post. Must retain at least Text or an ImageUrl.
/// </summary>
public class EditPostRequest
{
    [MaxLength(Post.MaxTextLength)]
    public string? Text { get; set; }

    [Url]
    public string? ImageUrl { get; set; }
}