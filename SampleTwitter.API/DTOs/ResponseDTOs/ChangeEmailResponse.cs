namespace SampleTwitter.API.DTOs.ResponseDTOs;

public record ChangeEmailResponse(
    long UserId,
    string Email,
    string Message
);
