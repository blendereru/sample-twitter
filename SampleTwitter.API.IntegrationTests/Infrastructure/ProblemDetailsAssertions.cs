using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace SampleTwitter.API.IntegrationTests.Infrastructure;

public static class ProblemDetailsAssertions
{
    /// <summary>
    /// Asserts the response has the given status code and a ProblemDetails body
    /// carrying the enriched fields (requestId, traceId, timestamp) added by
    /// CustomizeProblemDetails, and returns the deserialized body for further assertions.
    /// </summary>
    public static async Task<ProblemDetails> AssertProblemDetails(
        this HttpResponseMessage response,
        HttpStatusCode expectedStatusCode)
    {
        Assert.Equal(expectedStatusCode, response.StatusCode);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problemDetails);
        Assert.Equal((int)expectedStatusCode, problemDetails.Status);
        Assert.True(problemDetails.Extensions.ContainsKey("requestId"), "Expected 'requestId' in ProblemDetails.Extensions");
        Assert.True(problemDetails.Extensions.ContainsKey("traceId"), "Expected 'traceId' in ProblemDetails.Extensions");
        Assert.True(problemDetails.Extensions.ContainsKey("timestamp"), "Expected 'timestamp' in ProblemDetails.Extensions");

        return problemDetails;
    }

    /// <summary>
    /// Asserts the response is a 400 with a ValidationProblemDetails body, and that
    /// the given field name appears as a key in the Errors dictionary. Returns the
    /// deserialized body for further assertions (e.g. checking a specific error message).
    /// </summary>
    public static async Task<ValidationProblemDetails> AssertValidationProblemDetails(
        this HttpResponseMessage response,
        params string[] expectedInvalidFields)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.NotNull(problemDetails);
        Assert.Equal(StatusCodes.Status400BadRequest, problemDetails.Status);

        foreach (var field in expectedInvalidFields)
        {
            Assert.Contains(field, problemDetails.Errors.Keys);
        }

        return problemDetails;
    }
}