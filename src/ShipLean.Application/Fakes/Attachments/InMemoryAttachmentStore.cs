using ShipLean.Application.Features.WorkItemDetails.Attachments;
using ShipLean.Contracts;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Blobs;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Core;

namespace ShipLean.Application.Fakes.Attachments;

// Attachment rows in memory, bytes in the injected IBlobStore under an opaque ref the store makes up (a re-import writes a new ref and swaps to it once the put succeeds). Arrange the work
// items and users it knows with AddWorkItem and AddUser before use. Archiving marks the entry and leaves the blob.
// Publishes no events. An import never changes the archived mark.
public sealed class InMemoryAttachmentStore(ICurrentUser currentUser, IBlobStore blobs, TimeProvider time) : IAttachmentStore
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

    public Task<IReadOnlyList<AttachmentDto>> ListAsync(Guid workItemId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            RequireWorkItem(workItemId);
            IReadOnlyList<AttachmentDto> attachments = _entries
                .Where(e => e.WorkItemId == workItemId && !e.Archived)
                .OrderBy(e => e.CreatedAt).ThenBy(e => e.Sequence)
                .Select(ToDto)
                .ToList();
            return Task.FromResult(attachments);
        }
    }

    public async Task<AttachmentDto> AddAsync(Guid workItemId, string name, string contentType, Stream content, AttachmentSource? source = null, CancellationToken cancellationToken = default)
    {
        var cleanName = RequireName(name);
        lock (_gate)
        {
            RequireWorkItem(workItemId);
        }

        var id = Guid.NewGuid();
        var storageRef = NewRef();
        var size = await blobs.PutAsync(storageRef, content, contentType, cancellationToken);
        lock (_gate)
        {
            var entry = new Entry(id, workItemId, cleanName, contentType, size, storageRef, currentUser.Id, time.GetUtcNow(), _entries.Count) { Source = source };
            _entries.Add(entry);
            return ToDto(entry);
        }
    }

    public async Task<(AttachmentDto Attachment, BlobContent Content)> OpenAsync(Guid id, CancellationToken cancellationToken = default)
    {
        AttachmentDto dto;
        string storageRef;
        lock (_gate)
        {
            var entry = Live(id);
            dto = ToDto(entry);
            storageRef = entry.StorageRef;
        }

        var blob = await blobs.GetAsync(storageRef, cancellationToken) ?? throw ApiException.NotFound("That attachment does not exist.");
        return (dto, blob);
    }

    public Task ArchiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            Live(id).Archived = true;
            return Task.CompletedTask;
        }
    }

    public async Task<Guid> ImportAsync(Guid workItemId, ImportedAttachment attachment, Stream content, CancellationToken cancellationToken = default)
    {
        var cleanName = RequireName(attachment.Name);
        lock (_gate)
        {
            RequireWorkItem(workItemId);
            if (attachment.UploadedById is { } uploader && !_users.ContainsKey(uploader))
            {
                throw ApiException.NotFound("That user does not exist.");
            }
        }

        var storageRef = NewRef();
        var size = await blobs.PutAsync(storageRef, content, attachment.ContentType, cancellationToken);
        lock (_gate)
        {
            var existing = Find(workItemId, attachment);
            if (existing is null)
            {
                existing = new Entry(Guid.NewGuid(), workItemId, cleanName, attachment.ContentType, size, storageRef, attachment.UploadedById, attachment.CreatedAt, _entries.Count)
                {
                    OriginalSystem = attachment.OriginalSystem,
                    OriginalSystemId = attachment.OriginalSystemId,
                };
                _entries.Add(existing);
            }
            else
            {
                existing.Name = cleanName;
                existing.ContentType = attachment.ContentType;
                existing.SizeBytes = size;
                existing.StorageRef = storageRef;
            }

            return existing.Id;
        }
    }

    // Call inside the lock.
    private Entry? Find(Guid workItemId, ImportedAttachment attachment)
        => _entries.FirstOrDefault(e => e.WorkItemId == workItemId
            && e.OriginalSystem == attachment.OriginalSystem && e.OriginalSystemId == attachment.OriginalSystemId);

    private static string NewRef() => $"attachments/{Guid.NewGuid():N}";

    private static string RequireName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw ApiException.Validation("An attachment needs a name.");
        }

        return name.Trim();
    }

    private void RequireWorkItem(Guid workItemId)
    {
        if (!_workItems.Contains(workItemId))
        {
            throw ApiException.NotFound("That work item does not exist.");
        }
    }

    private Entry Live(Guid id)
        => _entries.FirstOrDefault(e => e.Id == id && !e.Archived) ?? throw ApiException.NotFound("That attachment does not exist.");

    private AttachmentDto ToDto(Entry entry)
        => new(
            entry.Id, entry.WorkItemId, entry.Name, entry.ContentType, entry.SizeBytes,
            entry.UploadedById is { } id && _users.TryGetValue(id, out var user) ? user : null,
            entry.CreatedAt, DisplayDate.Relative(entry.CreatedAt, time), $"/api/attachments/{entry.Id}/content",
            AttachmentKinds.Of(entry.Name, entry.ContentType), 0, entry.Source?.AgentName, entry.Source?.Via);

    private sealed class Entry(Guid id, Guid workItemId, string name, string contentType, long sizeBytes, string storageRef, Guid? uploadedById, DateTimeOffset createdAt, int sequence)
    {
        public Guid Id { get; } = id;

        public Guid WorkItemId { get; } = workItemId;

        public string Name { get; set; } = name;

        public string ContentType { get; set; } = contentType;

        public long SizeBytes { get; set; } = sizeBytes;

        public string StorageRef { get; set; } = storageRef;

        public Guid? UploadedById { get; } = uploadedById;

        public DateTimeOffset CreatedAt { get; } = createdAt;

        public int Sequence { get; } = sequence;

        public bool Archived { get; set; }

        public AttachmentSource? Source { get; init; }

        public string? OriginalSystem { get; init; }

        public string? OriginalSystemId { get; init; }
    }
}
