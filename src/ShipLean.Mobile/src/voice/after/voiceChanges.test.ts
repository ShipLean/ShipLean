import { describe, expect, it } from 'vitest';
import { VOICE_ROW_MS } from '../../actions/undo';
import { aPending } from '../../test-support/actions';
import { voiceChanges, type ItemChange } from './voiceChanges';

const ME = 'user-joel';
const SENT = '2030-01-01T09:00:00.000Z';
const at = (ms: number) => new Date(Date.parse(SENT) + ms);
const sent = (id: string, batchId = 'b1') =>
  aPending({ id, batchId, origin: 'voice', state: 'sent', createdAt: SENT, undoUntil: null });
const change = (ms: number, overrides: Partial<ItemChange> = {}): ItemChange => ({
  byId: ME,
  agentName: null,
  at: at(ms).toISOString(),
  ...overrides,
});

describe('voiceChanges', () => {
  it('a voice batch is undoable for 10 minutes after it was sent, then not', () => {
    const pending = [sent('p1'), sent('p2')];
    const before = voiceChanges(pending, 'item-1', [], ME, at(VOICE_ROW_MS - 1));
    expect(before).toHaveLength(1);
    expect(before[0]).toMatchObject({ batchId: 'b1', state: 'sent', undoable: true });
    expect(before[0].undoUntil).toBe(at(VOICE_ROW_MS).toISOString());
    expect(before[0].items.map((p) => p.id)).toEqual(['p1', 'p2']);

    const after = voiceChanges(pending, 'item-1', [], ME, at(VOICE_ROW_MS));
    expect(after[0]).toMatchObject({ undoable: false, changedSince: false });
  });

  it("another person's or an agent's later change ends the window early", () => {
    const now = at(60_000);
    const pending = [sent('p1')];
    // My own revisions (the voice change itself) and anything before the send keep the window open.
    const mine = [change(1_000), change(-5_000, { byId: 'user-priya' })];
    expect(voiceChanges(pending, 'item-1', mine, ME, now)[0]).toMatchObject({
      undoable: true,
      changedSince: false,
    });

    const priya = [...mine, change(30_000, { byId: 'user-priya' })];
    expect(voiceChanges(pending, 'item-1', priya, ME, now)[0]).toMatchObject({
      undoable: false,
      changedSince: true,
    });

    // An agent acting for me is still someone else, and so is a webhook (no person).
    const agent = [change(30_000, { agentName: 'Claude Code (claude.ai)' })];
    expect(voiceChanges(pending, 'item-1', agent, ME, now)[0].changedSince).toBe(true);
    const webhook = [change(30_000, { byId: null })];
    expect(voiceChanges(pending, 'item-1', webhook, ME, now)[0].changedSince).toBe(true);
  });

  it('batches from taps or typing are not voice changes', () => {
    const pending = [
      aPending({ id: 't1', batchId: 'tap', origin: 'tap', state: 'sent', createdAt: SENT }),
      aPending({ id: 't2', batchId: 'typed', origin: 'typed', state: 'sent', createdAt: SENT }),
      sent('v1', 'voice'),
    ];
    expect(voiceChanges(pending, 'item-1', [], ME, at(1_000)).map((c) => c.batchId)).toEqual([
      'voice',
    ]);
  });

  it('a queued batch is waiting with Undo and no window; a refused one keeps the rest as sent', () => {
    const waiting = [
      aPending({ id: 'q1', batchId: 'bq', origin: 'voice', state: 'queued', createdAt: SENT }),
    ];
    const refused = [
      sent('r1', 'br'),
      aPending({ id: 'r2', batchId: 'br', origin: 'voice', state: 'failed', createdAt: SENT }),
    ];
    const result = voiceChanges([...waiting, ...refused], 'item-1', [], ME, at(VOICE_ROW_MS * 2));
    expect(result).toEqual([
      expect.objectContaining({ batchId: 'bq', state: 'waiting', undoUntil: null, undoable: true }),
      expect.objectContaining({ batchId: 'br', state: 'refused', undoable: false }),
    ]);
  });

  it('a batch with any action gone out, or maybe gone out, is not waiting and has its window', () => {
    const partly = [
      sent('m1', 'bm'),
      aPending({ id: 'm2', batchId: 'bm', origin: 'voice', state: 'queued', createdAt: SENT }),
    ];
    const uncertain = [
      aPending({ id: 'u1', batchId: 'bu', origin: 'voice', state: 'uncertain', createdAt: SENT }),
    ];
    const sending = [
      aPending({ id: 's1', batchId: 'bs', origin: 'voice', state: 'sending', createdAt: SENT }),
    ];
    const result = voiceChanges([...partly, ...uncertain, ...sending], 'item-1', [], ME, at(1_000));
    const until = at(VOICE_ROW_MS).toISOString();
    expect(result).toEqual([
      expect.objectContaining({ batchId: 'bm', state: 'sent', undoUntil: until, undoable: true }),
      expect.objectContaining({ batchId: 'bu', state: 'sent', undoUntil: until, undoable: true }),
      expect.objectContaining({ batchId: 'bs', state: 'sent', undoUntil: until, undoable: true }),
    ]);
  });

  it('keeps only the batches on the item, and drops one already undone', () => {
    const other = aPending({
      id: 'o1',
      batchId: 'b-other',
      origin: 'voice',
      state: 'sent',
      createdAt: SENT,
      action: { area: 'workItem', kind: 'setState', itemId: 'item-2', key: 'SHIP-2' },
    });
    const undone = { ...sent('u1', 'b-undone'), undoOutcome: 'undone' as const };
    const pending = [sent('p1'), other, undone];
    expect(voiceChanges(pending, 'item-1', [], ME, at(1)).map((c) => c.batchId)).toEqual(['b1']);
    expect(voiceChanges(pending, null, [], ME, at(1)).map((c) => c.batchId)).toEqual([
      'b1',
      'b-other',
    ]);
  });
});
