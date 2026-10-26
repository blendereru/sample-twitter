using System.ComponentModel.DataAnnotations;
using SampleTwitter.API.Models;

namespace SampleTwitter.API.DTOs.RequestDTOs;

/// <summary>
/// Request to create a new post. At least one of Text or ImageUrl must be provided.
/// </summary>
public class CreatePostRequest
{
    [MaxLength(Post.MaxTextLength)]
    public string? Text { get; set; }

    [Url]
    public string? ImageUrl { get; set; }

    /// <summary>
    /// ID of the post being replied to. Omit for original top-level posts.
    /// </summary>
    public long? ReplyId { get; set; }
}