using Agoda.IoC.Core;
using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.WorkItemDetails.Comments;

// What the comment endpoints do: the access check first, then the store; a comment added here is a person's and may prompt a live agent session. Any member reads and writes comments; editing is
// the author's (the store enforces it), and archiving is the author's or a project admin's.
[RegisterPerRequest]
public sealed class CommentEndpoints(IProjectAccess access, ICommentStore store, ICurrentUser currentUser, NpgsqlDataSource dataSource, IAgentPrompts agentPrompts)
{
    public async Task<IReadOnlyList<CommentDto>> ListAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);
        return await store.ListAsync(workItemId, cancellationToken);
    }

    public async Task<CommentDto> AddAsync(Guid workItemId, CommentCreate create, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);
        var added = await store.AddAsync(workItemId, create, cancellationToken);
        // The one place a comment can prompt a session: a person writing through the app's own endpoint (the hook never throws).
        await agentPrompts.CommentAddedAsync(added.Id, cancellationToken);
        return added;
    }

    public async Task<CommentDto> EditAsync(Guid id, CommentEdit edit, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.Comment, id, ProjectRole.Member, cancellationToken);
        return await store.EditAsync(id, edit, cancellationToken);
    }

    public async Task<CommentDto> SupersedeAsync(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.Comment, id, ProjectRole.Member, cancellationToken);
        return await store.SupersedeAsync(id, cancellationToken);
    }

    public async Task ArchiveAsync(Guid id, CancellationToken cancellationToken)
    {
        var (_, role) = await access.RequireAsync(ProjectItem.Comment, id, ProjectRole.Member, cancellationToken);
        if (role != ProjectRole.Admin)
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            var authors = (await connection.QueryAsync<Guid?>(new CommandDefinition(
                "SELECT author_id FROM comment WHERE id = @id AND archived_at IS NULL", new { id }, cancellationToken: cancellationToken))).ToList();
            if (authors.Count == 0)
            {
                throw ApiException.NotFound("That comment does not exist.");
            }

            if (authors[0] != currentUser.Id)
            {
                throw ApiException.Forbidden("Only the author or a project admin can archive a comment.");
            }
        }

        await store.ArchiveAsync(id, cancellationToken);
    }
}
