import { describe, expect, it } from 'vitest';
import { designInbox } from './inboxDesign';

describe('designInbox', () => {
  it('holds the design data: 6 items and 15 asks', () => {
    const inbox = designInbox('p-1');
    const asks = inbox.items.flatMap((i) => i.asks);
    expect(inbox.projectId).toBe('p-1');
    expect(inbox.items.map((i) => i.key)).toEqual([
      'SHIP-743',
      'SHIP-892',
      'SHIP-907',
      'SHIP-924',
      'SHIP-742',
      'SHIP-906',
    ]);
    expect(inbox.items.find((i) => i.key === 'SHIP-924')?.underKey).toBe('SHIP-907');
    expect([inbox.openItems, inbox.openAsks, asks.length]).toEqual([6, 15, 15]);
    const count = (kind: string) => asks.filter((a) => a.kind === kind).length;
    expect([count('decision'), count('question'), count('mention'), count('status')]).toEqual([
      10, 3, 1, 1,
    ]);
    expect(inbox.items[2].asks.map((a) => a.label)).toEqual([
      'D1',
      'D2',
      'D3',
      'D4',
      'D5',
      'D6',
      'D7',
      'D8',
    ]);
    expect(inbox.items[5].asks.map((a) => a.label)).toEqual(['Q1', 'D10', 'D11']);
    expect(new Set(inbox.items.map((i) => i.id)).size).toBe(6);
  });
});
