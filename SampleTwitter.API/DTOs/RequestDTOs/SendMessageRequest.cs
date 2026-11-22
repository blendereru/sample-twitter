using System.ComponentModel.DataAnnotations;
using SampleTwitter.API.Models;

namespace SampleTwitter.API.DTOs.RequestDTOs;

public class SendMessageRequest
{
    [MaxLength(Message.MaxTextLength)]
    public string? Text { get; set; }
}