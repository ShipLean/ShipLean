using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Planning;

// One open code comment a report_task call answers.
public sealed record AnsweredComment
{
    [JsonPropertyName("id"), Description("The id of the code comment, from open_code_comments in the brief.")]
    public Guid? Id { get; init; }

    [JsonPropertyName("reply"), Description("What you did about it. It is posted as a reply and the comment is resolved.")]
    public string? Reply { get; init; }
}
