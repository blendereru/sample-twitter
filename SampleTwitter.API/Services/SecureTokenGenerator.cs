using System.Security.Cryptography;
using System.Text;
using SampleTwitter.API.Abstractions;

namespace SampleTwitter.API.Services;

public class SecureTokenGenerator : ISecureTokenGenerator
{
    private const int TokenSizeInBytes = 32;
    
    public string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenSizeInBytes);
        return Base64UrlEncode(bytes);
    }

    public string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}