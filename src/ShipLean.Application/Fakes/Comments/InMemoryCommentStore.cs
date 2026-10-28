using ShipLean.Application.Markdown;
using ShipLean.Contracts;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Core;

namespace ShipLean.Application.Fakes.Comments;

// Comments in memory, one lock around everything. Arrange the work items and users it knows with AddWorkItem and AddUser
// before use. Archiving marks the entry; nothing is removed. Publishes no events. An import never changes the archived mark.
public sealed class InMemoryCommentStore(ICurrentUser currentUser, MarkdownRenderer renderer, TimeProvider time, ICurrentAgent currentAgent) : ICommentStore
{
    private readonly object _gate = new();
    private readonly HashSet<Guid> _workItems = [];
    private readonly Dictionary<Guid, UserRef> _users = [];
    private readonly List<Entry> _entries = [];

    public void AddWorkItem(Guid workItemId)
    {
        lock (_gate)
        {
            _workItems.Add(workItemId);
        }
    }

    public void AddUser(UserRef user)
    {
        lock (_gate)
        {
            _users[user.Id] = user;
        }
    }

    public Task<IReadOnlyList<CommentDto>> ListAsync(Guid workItemId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            RequireWorkItem(workItemId);
            IReadOnlyList<CommentDto> comments = _entries
                .Where(e => e.WorkItemId == workItemId && !e.Archived)
                .OrderBy(e => e.CreatedAt).ThenBy(e => e.Sequence)
                .Select(ToDto)
                .ToList();
            return Task.FromResult(comments);
        }
    }

    public Task<CommentDto> AddAsync(Guid workItemId, CommentCreate create, CancellationToken cancellationToken = default)
        => Add(workItemId, create, AgentStamp.Of(currentAgent));

    public Task<CommentDto> AddAsAgentAsync(Guid workItemId, CommentCreate create, string agentName, CancellationToken cancellationToken = default)
        => string.IsNullOrWhiteSpace(agentName)
            ? throw new ArgumentException("An agent comment needs an agent name.", nameof(agentName))
            : Add(workItemId, create, agentName);

    private Task<CommentDto> Add(Guid workItemId, CommentCreate create, string? agentName)
    {
        var body = RequireBody(create.Body);
        lock (_gate)
        {
            RequireWorkItem(workItemId);
            var now = time.GetUtcNow();
            var entry = new Entry(Guid.NewGuid(), workItemId, currentUser.Id, body, now, _entries.Count) { AgentName = agentName };
            _entries.Add(entry);
            return Task.FromResult(ToDto(entry));
        }
    }

    public Task<CommentDto> EditAsync(Guid id, CommentEdit edit, CancellationToken cancellationToken = default)
    {
        var body = RequireBody(edit.Body);
        lock (_gate)
        {
            var entry = Live(id);
            if (entry.AuthorId != currentUser.Id)
            {
                throw ApiException.Forbidden("Only the author can edit a comment.");
            }

            entry.Body = body;
            entry.EditedAt = time.GetUtcNow();
            return Task.FromResult(ToDto(entry));
        }
    }

    public Task<CommentDto> SupersedeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var entry = Live(id);
            entry.SupersededAt ??= time.GetUtcNow();
            entry.SupersededBy ??= currentUser.Id;
            return Task.FromResult(ToDto(entry));
        }
    }

    public Task ArchiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            Live(id).Archived = true;
            return Task.CompletedTask;
        }
    }

    public Task<Guid> ImportAsync(Guid workItemId, ImportedComment comment, CancellationToken cancellationToken = default)
    {
        var body = RequireBody(comment.Body);
        lock (_gate)
        {
            RequireWorkItem(workItemId);
            if (comment.AuthorId is { } author && !_users.ContainsKey(author))
            {
                throw ApiException.NotFound("That user does not exist.");
            }

            var existing = _entries.FirstOrDefault(e => e.WorkItemId == workItemId
                && e.OriginalSystem == comment.OriginalSystem && e.OriginalSystemId == comment.OriginalSystemId);
            if (existing is null)
            {
                existing = new Entry(Guid.NewGuid(), workItemId, comment.AuthorId, body, comment.CreatedAt, _entries.Count)
                {
                    OriginalSystem = comment.OriginalSystem,
                    OriginalSystemId = comment.OriginalSystemId,
                };
                _entries.Add(existing);
            }
            else
            {
                existing.Body = body;
            }

            return Task.FromResult(existing.Id);
        }
    }

    private static string RequireBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            throw ApiException.Validation("A comment cannot be empty.");
        }

        return body;
    }

    private void RequireWorkItem(Guid workItemId)
    {
        if (!_workItems.Contains(workItemId))
        {
            throw ApiException.NotFound("That work item does not exist.");
        }
    }

    private Entry Live(Guid id)
        => _entries.FirstOrDefault(e => e.Id == id && !e.Archived) ?? throw ApiException.NotFound("That comment does not exist.");

    // Call inside the lock.
    private CommentDto ToDto(Entry entry)
    {
        var rendered = renderer.Render(entry.Body);
        UserRef? author = entry.AuthorId is { } id && _users.TryGetValue(id, out var user) ? user : null;
        var superseded = entry.SupersededAt is { } at && entry.SupersededBy is { } by && _users.TryGetValue(by, out var marker)
            ? new CommentSuperseded(at, DisplayDate.Relative(at, time), marker)
            : null;
        return new CommentDto(
            entry.Id, entry.WorkItemId, author, rendered.Markdown, rendered.Html,
            entry.CreatedAt, DisplayDate.Relative(entry.CreatedAt, time),
            entry.EditedAt, entry.EditedAt is { } edited ? DisplayDate.Relative(edited, time) : null,
            entry.AgentName, superseded);
    }

    private sealed class Entry(Guid id, Guid workItemId, Guid? authorId, string body, DateTimeOffset createdAt, int sequence)
    {
        public Guid Id { get; } = id;

        public Guid WorkItemId { get; } = workItemId;

        public Guid? AuthorId { get; } = authorId;

        public string Body { get; set; } = body;

        public DateTimeOffset CreatedAt { get; } = createdAt;

        public int Sequence { get; } = sequence;

        public DateTimeOffset? EditedAt { get; set; }

        public bool Archived { get; set; }

        public DateTimeOffset? SupersededAt { get; set; }

        public Guid? SupersededBy { get; set; }

        public string? AgentName { get; init; }

        public string? OriginalSystem { get; init; }

        public string? OriginalSystemId { get; init; }
    }
}
