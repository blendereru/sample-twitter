using System.Security.Claims;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SampleTwitter.API.Abstractions;
using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.DTOs.ResponseDTOs;

namespace SampleTwitter.API.Controllers;

[Tags("Users")]
[Route("api/users")]
[ApiController]
public class UsersController : ControllerBase
{
    private readonly IPostService _postService;
    private readonly IAccountService _accountService;

    public UsersController(IPostService postService, IAccountService accountService)
    {
        _postService = postService;
        _accountService = accountService;
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
        var viewerUserId = GetCurrentUserIdOrNull();
        var feed = await _postService.GetPosts(userId, viewerUserId, ct);

        var items = feed.Items
            .Select(i => new PostFeedItemDto(
                i.Id,
                i.Text,
                i.ImageUrl,
                i.CreatedAt,
                i.UpdatedAt,
                i.RepostCount,
                i.ReplyCount,
                i.IsBookmarked,
                i.IsReposted))
            .ToList();

        return Ok(new PostFeedResponse(items, new PostAuthorDto(feed.Author.Id, feed.Author.Email)));
    }

    /// <summary>
    /// Returns the profile replies feed for the given user.
    /// </summary>
    /// <param name="userId">The id of the user whose feed to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Feed returned successfully.</response>
    /// <response code="404">User is not found (an account was deleted or invalid userId)</response>
    /// <response code="500">An unexpected error occurred while processing the request.</response>
    [HttpGet("{userId:long}/replies")]
    [ProducesResponseType(typeof(ReplyFeedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetProfileReplies(long userId, CancellationToken ct)
    {
        var viewerUserId = GetCurrentUserIdOrNull();
        var feed = await _postService.GetProfileReplies(userId, viewerUserId, ct);
        return Ok(feed.Adapt<ReplyFeedResponse>());
    }

    /// <summary>
    /// Returns the bookmarked posts feed for the currently authenticated user.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Bookmarks feed returned successfully.</response>
    /// <response code="401">The request is not authenticated (no valid auth cookie).</response>
    /// <response code="404">Authenticated user no longer exists.</response>
    /// <response code="500">An unexpected error occurred while processing the request.</response>
    [Authorize]
    [HttpGet("bookmarks")]
    [ProducesResponseType(typeof(List<PostResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetBookmarks(CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var currentUserId = long.Parse(userIdClaim!);

        var results = await _postService.GetBookmarks(currentUserId, ct);

        var response = results.Select(b => new PostResponse(
            b.Id,
            b.Text,
            b.ImageUrl,
            b.CreatedAt,
            b.UpdatedAt,
            new PostAuthorDto(b.Author.Id, b.Author.Email),
            b.ReplyId,
            b.RepostCount,
            b.ReplyCount,
            IsBookmarked: true,
            b.IsReposted,
            b.ReplyToAuthor is null
                ? null
                : new PostAuthorDto(b.ReplyToAuthor.Id, b.ReplyToAuthor.Email))).ToList();

        return Ok(response);
    }

    /// <summary>
    /// Returns the user info for the given user.
    /// </summary>
    /// <param name="id">The ID of the user to retrieve info about</param>
    /// <param name="ct">Cancellation token</param>
    /// <response code="200">User returned successfully</response>
    /// <response code="404">User is not found (an account was deleted or invalid userId)</response>
    /// <response code="500">An unexpected error occurred while processing the request.</response>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetUser(long id, CancellationToken ct)
    {
        var user = await _accountService.GetUserById(id, ct);
        return Ok(new UserResponse(user.Id, user.Email, user.RegisteredAt, user.Username, user.ImageUrl, user.UpdatedAt));
    }

    /// <summary>
    /// Edits the profile settings (username and/or profile image) for the given user.
    /// Only the user themselves can edit their profile, and at least a username or image URL must be retained.
    /// </summary>
    /// <param name="id">The ID of the user whose profile to edit.</param>
    /// <param name="request">The updated username and/or image URL.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">The profile was updated successfully; returns the modified profile representation.</response>
    /// <response code="400">The request body failed validation (e.g. username exceeds 50 characters, empty profile, or invalid image URL).</response>
    /// <response code="401">The request is not authenticated (no valid auth cookie).</response>
    /// <response code="403">The authenticated user is attempting to edit another user's profile.</response>
    /// <response code="404">No user exists with the given ID.</response>
    /// <response code="409">The specified username is already taken by another account.</response>
    /// <response code="500">An unexpected error occurred while processing the request.</response>
    [Authorize]
    [HttpPut("{id:long}")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(EditProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> EditProfile(long id, EditProfileRequest request, CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var currentUserId = long.Parse(userIdClaim!);

        var result = await _accountService.Edit(id, request, currentUserId, ct);

        return Ok(new EditProfileResponse(result.Id, result.Username, result.ImageUrl, result.UpdatedAt));
    }

    private long? GetCurrentUserIdOrNull()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}