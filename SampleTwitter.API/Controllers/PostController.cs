using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SampleTwitter.API.Abstractions;
using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.DTOs.ResponseDTOs;

namespace SampleTwitter.API.Controllers;

[Tags("Posts")]
[Route("api/posts")]
[ApiController]
public class PostController : ControllerBase
{
    private readonly IPostService _postService;
    private readonly ILogger<PostController> _logger;

    public PostController(IPostService postService, ILogger<PostController> logger)
    {
        _postService = postService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new post. The post must contain at least text or an image.
    /// If <paramref name="request"/> includes a <c>ReplyId</c>, the referenced post must exist.
    /// </summary>
    /// <response code="201">The post was created successfully.</response>
    /// <response code="400">The request body failed validation (e.g. text exceeds 280 characters, empty post, or invalid image URL).</response>
    /// <response code="401">The request is not authenticated (no valid auth cookie).</response>
    /// <response code="404">The post referenced by ReplyId was not found.</response>
    /// <response code="500">An unexpected error occurred while processing the request.</response>
    [Authorize]
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(CreatePostResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create(CreatePostRequest request, CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userId = long.Parse(userIdClaim!);

        var result = await _postService.Create(request, userId, ct);

        var response = new CreatePostResponse(result.Id, "Post created successfully.");

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, response);
    }

    /// <summary>
    /// Edits an existing post. Only the post's author can edit it.
    /// The post must still contain at least text or an image after the edit.
    /// </summary>
    /// <param name="id">The ID of the post to edit.</param>
    /// <param name="request">The new text and/or image URL for the post.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">The post was updated successfully.</response>
    /// <response code="400">The request body failed validation (e.g. text exceeds 280 characters, empty post, or invalid image URL).</response>
    /// <response code="401">The request is not authenticated (no valid auth cookie).</response>
    /// <response code="403">The authenticated user is not the author of this post.</response>
    /// <response code="404">No post exists with the given ID.</response>
    /// <response code="500">An unexpected error occurred while processing the request.</response>
    [Authorize]
    [HttpPut("{id}")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(EditPostResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Edit(long id, EditPostRequest request, CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userId = long.Parse(userIdClaim!);

        var result = await _postService.Edit(id, request, userId, ct);

        return Ok(new EditPostResponse(result.Id, "Post updated successfully."));
    }

    /// <summary>
    /// Deletes an existing post. Only the post's author can delete it.
    /// </summary>
    /// <param name="id">The ID of the post to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="204">The post was deleted successfully.</response>
    /// <response code="401">The request is not authenticated (no valid auth cookie).</response>
    /// <response code="403">The authenticated user is not the author of this post.</response>
    /// <response code="404">No post exists with the given ID.</response>
    /// <response code="500">An unexpected error occurred while processing the request.</response>
    [Authorize]
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userId = long.Parse(userIdClaim!);

        await _postService.Delete(id, userId, ct);

        return NoContent();
    }

    /// <summary>
    /// Retrieves a post by its ID.
    /// </summary>
    /// <response code="200">The post was found.</response>
    /// <response code="404">No post exists with the given ID.</response>
    /// <response code="500">An unexpected error occurred while processing the request.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetById(long id)
    {
        throw new NotImplementedException();
    }
}