// The voice changes on one item after Apply (voice D16, D19, D20): pure, so the window and "someone else changed it"
// are decided from the runner's pending() and the item's history alone.
import type { HistoryDto } from '@shiplean/core/api/workItems';
import { VOICE_ROW_MS } from '../../actions/undo';
import type { Pending } from '../../actions/types';

// One change to the item, from its history: who made it, through which agent, and when (ISO 8601 UTC).
export interface ItemChange {
  byId: string | null;
  agentName: string | null;
  at: string;
}

// waiting: none of it has gone out yet (every action queued, or still in its undo toast). sent: some or all of it went
// out, or may have (sent, sending or uncertain). refused: ShipLean refused part of it when it was sent.
export type VoiceChangeState = 'waiting' | 'sent' | 'refused';

export interface VoiceChange {
  batchId: string;
  // The batch's actions, in dispatch order.
  items: Pending[];
  state: VoiceChangeState;
  // When Undo on the row ends (ISO 8601 UTC); null while it is waiting, which has no window.
  undoUntil: string | null;
  // Someone else (another person, an agent or a webhook) changed the item after it was sent.
  changedSince: boolean;
  undoable: boolean;
}

// Every revision and ask line in the item's history, as changes.
export function historyChanges(history: HistoryDto): ItemChange[] {
  return [
    ...history.revisions.map((r) => ({
      byId: r.changedBy?.id ?? null,
      agentName: r.agentName,
      at: r.changedAt,
    })),
    ...history.asks.map((a) => ({ byId: a.by?.id ?? null, agentName: a.agentName, at: a.at })),
  ];
}

function stateOf(items: Pending[]): VoiceChangeState {
  if (items.some((p) => p.state === 'failed')) return 'refused';
  if (items.every((p) => p.state === 'queued' || p.state === 'undo')) return 'waiting';
  return 'sent';
}

const isSomeoneElse = (c: ItemChange, meId: string) => c.agentName !== null || c.byId !== meId;

// The voice batches that touch the item (every item when itemId is null), oldest first, leaving out any batch that
// has been undone. A sent batch's window runs VOICE_ROW_MS from its dispatch (Pending has no send time; a voice batch
// is sent at once when online), and a later change by anyone but me ends it early.
export function voiceChanges(
  pending: Pending[],
  itemId: string | null,
  changes: ItemChange[],
  meId: string,
  now: Date,
): VoiceChange[] {
  const batches = new Map<string, Pending[]>();
  for (const p of pending) {
    if (p.origin !== 'voice') continue;
    batches.set(p.batchId, [...(batches.get(p.batchId) ?? []), p]);
  }
  const result: VoiceChange[] = [];
  for (const [batchId, items] of batches) {
    if (itemId !== null && !items.some((p) => p.action.itemId === itemId)) continue;
    if (items.some((p) => p.undoOutcome !== null)) continue;
    const state = stateOf(items);
    if (state === 'waiting') {
      result.push({ batchId, items, state, undoUntil: null, changedSince: false, undoable: true });
      continue;
    }
    const sentAt = Date.parse(items[0].createdAt);
    const until = sentAt + VOICE_ROW_MS;
    const changedSince = changes.some((c) => Date.parse(c.at) > sentAt && isSomeoneElse(c, meId));
    result.push({
      batchId,
      items,
      state,
      undoUntil: new Date(until).toISOString(),
      changedSince,
      undoable: !changedSince && now.getTime() < until,
    });
  }
  return result;
}
