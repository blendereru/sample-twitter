using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SampleTwitter.API.Abstractions;
using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.DTOs.ResponseDTOs;

namespace SampleTwitter.API.Controllers;

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

        var post = await _postService.Create(request, userId, ct);

        var response = new CreatePostResponse(post.Id, "Post created successfully.");

        return CreatedAtAction(nameof(GetById), new { id = post.Id }, response);
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