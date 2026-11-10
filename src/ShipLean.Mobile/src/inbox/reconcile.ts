// Whether an answer whose send got no response went through (P5), read from a fresh Inbox: it did when none of the asks
// it answers is still open on its item.
import type { InboxDto } from '@shiplean/core/api/inbox';
import { labelsOf, type InboxAction } from './actions';

export function reconcileAnswer(action: InboxAction, inbox: InboxDto): 'sent' | 'notSent' {
  const item = inbox.items.find((i) => i.id === action.itemId);
  const open = new Set(item?.asks.map((a) => a.label) ?? []);
  return labelsOf(action).some((label) => open.has(label)) ? 'notSent' : 'sent';
}
