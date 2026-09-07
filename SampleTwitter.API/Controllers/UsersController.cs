using Microsoft.AspNetCore.Mvc;
using SampleTwitter.API.Abstractions;
using SampleTwitter.API.DTOs.ResponseDTOs;

namespace SampleTwitter.API.Controllers;

[Route("api/users")]
[ApiController]
public class UsersController : ControllerBase
{
    private readonly IPostService _postService;

    public UsersController(IPostService postService)
    {
        _postService = postService;
    }

    /// <summary>
    /// Returns the profile Posts feed for the given user.
    /// Includes the user's own top-level posts and replies to their own posts (self-threads).
    /// Replies to other users' posts are excluded (those belong to the Replies tab).
    /// Results are ordered newest first.
    /// </summary>
    /// <param name="userId">The ID of the user whose feed to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Feed returned successfully.</response>
    /// <response code="404">User is not found (account was deleted or invalid userId)</response>
    /// <response code="500">An unexpected error occurred while processing the request.</response>
    [HttpGet("{userId}/posts")]
    [ProducesResponseType(typeof(PostFeedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetProfilePosts(long userId, CancellationToken ct)
    {
        var feed = await _postService.GetProfileFeed(userId, ct);
        return Ok(feed);
    }
}
