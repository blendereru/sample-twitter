using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.DTOs.ResponseDTOs;
using SampleTwitter.API.IntegrationTests.Infrastructure;

namespace SampleTwitter.API.IntegrationTests.Images;

public class UploadImageTests : IntegrationTestBase
{
    public UploadImageTests(ApiWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Upload_ValidPngWhenAuthenticated_Returns201AndServesStaticFileAndCanBeUsedInPost()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("uploader@example.com", "Sup3rSecret1!");
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x11, 0x22, 0x33, 0x44 };

        using var content = CreateMultipartImageContent(pngBytes, "avatar.png", "image/png");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/images")
        {
            Content = content
        };
        request.Headers.Add("Cookie", cookie);

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<UploadImageResponse>();
        Assert.NotNull(body);
        Assert.StartsWith("http://localhost/uploads/", body.Url);
        Assert.EndsWith(".png", body.Url);

        try
        {
            var relativeUri = new Uri(body.Url).PathAndQuery;
            var staticResponse = await Client.GetAsync(relativeUri);
            Assert.Equal(HttpStatusCode.OK, staticResponse.StatusCode);
            var downloadedBytes = await staticResponse.Content.ReadAsByteArrayAsync();
            Assert.Equal(pngBytes, downloadedBytes);

            using var postRequest = new HttpRequestMessage(HttpMethod.Post, "/api/posts")
            {
                Content = JsonContent.Create(new CreatePostRequest
                {
                    Text = "Post with uploaded image",
                    ImageUrl = body.Url
                })
            };
            postRequest.Headers.Add("Cookie", cookie);

            var postResponse = await Client.SendAsync(postRequest);
            Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);
        }
        finally
        {
            DeleteUploadedFileIfExists(body.Url);
        }
    }

    [Fact]
    public async Task Upload_WhenUnauthenticated_Returns401Unauthorized()
    {
        // Arrange
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        using var content = CreateMultipartImageContent(pngBytes, "avatar.png", "image/png");

        // Act
        var response = await Client.PostAsync("/api/images", content);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Upload_InvalidFileSignature_Returns400BadRequest()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("uploader2@example.com", "Sup3rSecret1!");
        var fakeBytes = "Not an image"u8.ToArray();

        using var content = CreateMultipartImageContent(fakeBytes, "spoofed.png", "image/png");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/images")
        {
            Content = content
        };
        request.Headers.Add("Cookie", cookie);

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Invalid image", problem.Title);
    }

    private static MultipartFormDataContent CreateMultipartImageContent(
        byte[] bytes,
        string fileName,
        string contentType)
    {
        var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "file", fileName);
        return multipart;
    }

    private void DeleteUploadedFileIfExists(string url)
    {
        var env = Factory.Services.GetRequiredService<IWebHostEnvironment>();
        var webRoot = string.IsNullOrWhiteSpace(env.WebRootPath)
            ? Path.Combine(env.ContentRootPath, "wwwroot")
            : env.WebRootPath;
        var fileName = Path.GetFileName(new Uri(url).LocalPath);
        var filePath = Path.Combine(webRoot, "uploads", fileName);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
}