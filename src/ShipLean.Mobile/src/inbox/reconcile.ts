// Whether an answer whose send got no response went through (P5), read from a fresh Inbox.
// - A decision or question answer went through when none of the asks it answers is still open on its item.
// - A mention reply or seen went through when that mention is no longer open. Mentions are renumbered per read, so the
//   mention id decides, never its label.
// - A discussion never closes its decision, so it went through when the caller's own comment with that body is on the
//   item, posted since the answer was dispatched.
import type { InboxDto } from '@shiplean/core/api/inbox';
import type { CommentDto } from '@shiplean/core/api/comments';
import { labelsOf } from './actions';
import type { InboxPending } from './reviewBatch';

// The device clock that stamped Pending.createdAt and the server clock that stamped the comment can differ.
const CLOCK_SKEW_MS = 2 * 60 * 1000;

export interface DiscussionCheck {
  callerId: string;
  // The comments of the answer's item.
  comments: CommentDto[];
}

export function reconcileAnswer(
  pending: InboxPending,
  inbox: InboxDto,
  discussion: DiscussionCheck,
): 'sent' | 'notSent' {
  const a = pending.action;
  const asks = inbox.items.find((i) => i.id === a.itemId)?.asks ?? [];
  if (a.kind === 'mentionReply' || a.kind === 'seen')
    return asks.some((ask) => ask.mention?.mentionId === a.mentionId) ? 'notSent' : 'sent';
  if (a.kind === 'discuss') {
    const since = Date.parse(pending.createdAt) - CLOCK_SKEW_MS;
    const landed = discussion.comments.some(
      (c) =>
        c.author?.id === discussion.callerId &&
        c.body === a.body &&
        Date.parse(c.createdAt) >= since,
    );
    return landed ? 'sent' : 'notSent';
  }
  const open = new Set(asks.map((ask) => ask.label));
  return labelsOf(a).some((label) => open.has(label)) ? 'notSent' : 'sent';
}
