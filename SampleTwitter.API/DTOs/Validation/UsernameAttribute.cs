using System.ComponentModel.DataAnnotations;
using SampleTwitter.API.Models;

namespace SampleTwitter.API.DTOs.Validation;

/// <summary>
/// Validates a username against the rules defined by <see cref="User.GetUsernameValidationError"/>.
/// Null or whitespace values are considered valid; combine with <see cref="RequiredAttribute"/> when the value is mandatory.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class UsernameAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null || value is string text && string.IsNullOrWhiteSpace(text))
        {
            return ValidationResult.Success;
        }

        var memberNames = validationContext.MemberName is null ? null : new[] { validationContext.MemberName };

        if (value is not string username)
        {
            return new ValidationResult("Username must be a string.", memberNames);
        }

        var error = User.GetUsernameValidationError(username);
        return error is null ? ValidationResult.Success : new ValidationResult(error, memberNames);
    }
}