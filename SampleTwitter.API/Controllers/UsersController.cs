using Microsoft.AspNetCore.Mvc;
using SampleTwitter.API.Abstractions;
using SampleTwitter.API.DTOs.ResponseDTOs;

namespace SampleTwitter.API.Controllers;

[Tags("Users")]
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
    /// This only shows a user's own posts.
    /// </summary>
    /// <param name="userId">The ID of the user whose feed to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Feed returned successfully.</response>
    /// <response code="404">User is not found (an account was deleted or invalid userId)</response>
    /// <response code="500">An unexpected error occurred while processing the request.</response>
    [HttpGet("{userId:long}/posts")]
    [ProducesResponseType(typeof(PostFeedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetProfilePosts(long userId, CancellationToken ct)
    {
        var feed = await _postService.GetPosts(userId, ct);
        return Ok(new PostFeedResponse(feed.Items, feed.Author));
    }
}