using System.ComponentModel.DataAnnotations;

namespace SampleTwitter.API.DTOs.RequestDTOs;

public class EditPostRequest
{
    [MaxLength(280)]
    public string? Text { get; set; }

    [Url]
    public string? ImageUrl { get; set; }
}