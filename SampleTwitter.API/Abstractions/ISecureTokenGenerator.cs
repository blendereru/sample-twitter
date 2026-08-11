namespace SampleTwitter.API.Abstractions;

public interface ISecureTokenGenerator
{
    string Generate();
    string Hash(string token);
}