using System.Security.Cryptography;
using System.Text;

namespace ShipLean.Contracts.Prompts;

public static class PromptHash
{
    // Lowercase hex SHA-256 of the UTF-8 text with "\r\n" turned into "\n".
    public static string Of(string text)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n", StringComparison.Ordinal))));
}
