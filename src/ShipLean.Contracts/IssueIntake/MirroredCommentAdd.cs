using ShipLean.Contracts.Events;

namespace ShipLean.Contracts.IssueIntake;

// What IMirroredComments.AddAsync did. CommentId is the ShipLean comment mirrored from the GitHub comment: the new one, or the
// existing one when that GitHub comment was already mirrored. Changed is the commentsChanged event the caller publishes through
// IProjectEvents after its transaction commits (never before, never after a rollback); null when nothing was added.
public sealed record MirroredCommentAdd(Guid CommentId, CommentsChanged? Changed);
