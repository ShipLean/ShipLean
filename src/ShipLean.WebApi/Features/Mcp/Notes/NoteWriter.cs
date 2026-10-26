using Agoda.IoC.Core;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Errors;

namespace ShipLean.WebApi.Features.Mcp.Notes;

// The note a tool leaves after its change has committed (#13 principle 1: the change stands; a note that fails is reported,
// not thrown). This is the one place in the MCP tools that catches ApiException: the tool-error filter maps what escapes a
// tool, but here the failure must become part of a successful result instead.
[RegisterPerRequest]
public sealed class NoteWriter(ICommentStore comments)
{
    public async Task<NoteOutcome> AddAsync(Guid itemId, string note, CancellationToken cancellationToken)
    {
        try
        {
            await comments.AddAsync(itemId, new CommentCreate(note.Trim()), cancellationToken);
            return new NoteOutcome(true, null);
        }
        catch (ApiException e)
        {
            return new NoteOutcome(false, e.Message);
        }
    }
}
