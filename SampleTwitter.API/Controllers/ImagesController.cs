using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SampleTwitter.API.Abstractions;
using SampleTwitter.API.DTOs.ResponseDTOs;
using SampleTwitter.API.Exceptions;

namespace SampleTwitter.API.Controllers;

[Tags("Images")]
[Route("api/images")]
[ApiController]
public class ImagesController : ControllerBase
{
    private readonly IImageStorage _imageStorage;
    private readonly ILogger<ImagesController> _logger;

    public ImagesController(IImageStorage imageStorage, ILogger<ImagesController> logger)
    {
        _imageStorage = imageStorage;
        _logger = logger;
    }

    /// <summary>
    /// Uploads an image file (JPG, PNG, GIF, or WEBP, max 5 MB) and returns its public URL.
    /// </summary>
    /// <param name="file">The image file sent as multipart/form-data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="201">The image was uploaded successfully.</response>
    /// <response code="400">The file is missing, empty, exceeds 5 MB, or is not a valid image format.</response>
    /// <response code="401">The request is not authenticated.</response>
    /// <response code="500">An unexpected error occurred while processing the request.</response>
    [Authorize]
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(UploadImageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            throw new InvalidImageException(
                "Uploaded image file is null or empty.",
                "Please select a non-empty image file to upload.");
        }

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var extension = Path.GetExtension(file.FileName);

        await using var stream = file.OpenReadStream();
        var relativePath = await _imageStorage.Save(stream, extension, ct);

        var url = $"{Request.Scheme}://{Request.Host}{relativePath}";
        _logger.LogInformation("User {UserId} uploaded image {Url}", userIdClaim, url);

        return Created(url, new UploadImageResponse(url));
    }
}