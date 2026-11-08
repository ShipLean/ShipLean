using System.Diagnostics.CodeAnalysis;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.Proposal;

// Parses a proposal agent's response (C2): public Markdown, then one final ```shiplean-proposal block holding the JSON.
public static class IssueProposalParser
{
    public const string BlockTag = "shiplean-proposal";

    public static bool TryParse(string response, [NotNullWhen(true)] out IssueProposal? proposal, out string publicMarkdown) =>
        TryParse(response, out proposal, out publicMarkdown, out _);

    // failure says why the response was refused, for the job's last_error; it is null on success.
    public static bool TryParse(
        string response, [NotNullWhen(true)] out IssueProposal? proposal, out string publicMarkdown, out string? failure)
    {
        // TODO: the parser replaces this.
        throw new NotSupportedException("The proposal parser is not built yet.");
    }
}
