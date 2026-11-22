using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros.Analysis;

// SHA-256 of the arguments serialized in their fixed property order: the same arguments always hash alike and any changed one does not.
internal static class RetroAnalysisDigest
{
    public static string Of(RecordRetroAnalysisInput input)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(input))));
}
