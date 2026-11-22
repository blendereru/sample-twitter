using System.ComponentModel.DataAnnotations;

namespace SampleTwitter.API.DTOs.RequestDTOs;

public class StartConversationRequest
{
    [Range(1, long.MaxValue)]
    public long RecipientId { get; set; }
}