using System.Security.Cryptography;
using SampleTwitter.API.Abstractions;

namespace SampleTwitter.API.Services;

public class UsernameGenerator : IUsernameGenerator
{
    public const string Prefix = "user_";
    public const int SuffixLength = 8;
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";

    public string Generate() => Prefix + RandomNumberGenerator.GetString(Alphabet, SuffixLength);
}