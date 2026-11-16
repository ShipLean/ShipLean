using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Code;

// One commit an agent asks add_commits to put on a Task or Bug.
public sealed record CommitInput
{
    [JsonPropertyName("repo"), Description("The repository exactly as git remote get-url origin prints it, such as git@github.com:acme/storefront.git or https://github.com/acme/storefront.")]
    public string? Repo { get; init; }

    [JsonPropertyName("sha"), Description("The full commit sha (at least 7 hex characters work), or the commit's URL when repo is left out.")]
    public string? Sha { get; init; }
}
