namespace SampleTwitter.API.DTOs.ResponseDTOs;

/// <summary>
/// Representation of a follow relationship returned when a user follows another user.
/// </summary>
public record FollowResponse(long FollowerId, long FollowingId, DateTimeOffset CreatedAt);