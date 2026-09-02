using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.Models;

namespace SampleTwitter.API.Abstractions;

public interface IPostService
{
    Task<Post> Create(CreatePostRequest request, long userId, CancellationToken ct = default);
}