using System.ComponentModel.DataAnnotations;

namespace SampleTwitter.API.DTOs.RequestDTOs;

public class CreatePostRequest
{
    [MaxLength(280)]
    public string? Text { get; set; }

    [Url]
    public string? ImageUrl { get; set; }

    public long? ReplyId { get; set; }
}