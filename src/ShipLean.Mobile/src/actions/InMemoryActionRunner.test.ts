import { describe, expect, it, vi } from 'vitest';
import { InMemoryActionRunner } from './InMemoryActionRunner';
import type { AppAction, Executor } from './types';
import { UNDO } from './undo';

const T0 = Date.parse('2030-01-01T09:00:00.000Z');

function clock() {
  let at = T0;
  return { now: () => new Date(at), advance: (ms: number) => (at += ms) };
}

const action = (itemId: string): AppAction => ({
  area: 'workItem',
  kind: 'setState',
  itemId,
  key: null,
});

const executor = (area: AppAction['area'] = 'workItem'): Executor<AppAction> => ({
  area,
  send: async () => undefined,
  describe: (a) => ({ id: `${a.kind}-${a.itemId}`, icon: 'state', text: a.kind, removable: true }),
  reversible: () => true,
});

describe('InMemoryActionRunner', () => {
  it('holds a dispatched batch in undo until its window ends', () => {
    const time = clock();
    const runner = new InMemoryActionRunner({ now: time.now });
    runner.dispatch([action('1'), action('2')], {
      undo: UNDO.tap,
      origin: 'tap',
      allOrNothing: true,
    });

    const held = runner.pending();
    expect(held.map((p) => p.state)).toEqual(['undo', 'undo']);
    expect(held[0].undoUntil).toBe(new Date(T0 + 5000).toISOString());

    time.advance(4999);
    expect(runner.pending().map((p) => p.state)).toEqual(['undo', 'undo']);
    time.advance(1);
    expect(runner.pending().map((p) => p.state)).toEqual(['queued', 'queued']);
  });

  it('removes a batch on undo and notifies subscribers', async () => {
    const runner = new InMemoryActionRunner({ now: clock().now });
    const kept = runner.dispatch([action('1')], {
      undo: UNDO.tap,
      origin: 'tap',
      allOrNothing: true,
    });
    const undone = runner.dispatch([action('2'), action('3')], {
      undo: UNDO.voice,
      origin: 'voice',
      allOrNothing: true,
    });
    const ids = runner
      .pending()
      .filter((p) => p.batchId === undone)
      .map((p) => p.id);
    const changed = vi.fn();
    runner.subscribe(changed);

    const outcomes = await runner.undo(undone);

    expect(outcomes).toEqual([
      { id: ids[1], outcome: 'undone' },
      { id: ids[0], outcome: 'undone' },
    ]);
    expect(runner.pending().map((p) => p.batchId)).toEqual([kept]);
    expect(changed).toHaveBeenCalled();
  });

  it('ignores the same executor twice and rejects another for its area', () => {
    const runner = new InMemoryActionRunner({ now: clock().now });
    const first = executor();
    runner.register(first);
    runner.register(first);
    expect(runner.describe(action('1')).id).toBe('setState-1');

    expect(() => runner.register(executor())).toThrow(/already registered for workItem/);
    runner.register(executor('retro'));
  });
});
