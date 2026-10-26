using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShipLean.WebApi.Features.Mcp.Tools.Planning;

namespace ShipLean.WebApi.Features.Mcp.Planning.Capture;

// SHA-256 of the bound arguments (never the plan token or the project, which the seal and the envelope carry) serialized with
// their fixed property order, so the same arguments always hash alike and any changed one does not.
public static class CapturePlanDigest
{
    public static string Of(CapturePlanInput input)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(input))));
}
