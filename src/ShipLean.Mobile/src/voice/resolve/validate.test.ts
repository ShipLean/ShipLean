import { describe, expect, it } from 'vitest';
import { aTranscript } from '../../test-support/voice';
import type { VoiceActionSpec } from '../contracts';
import { checkCall, fits } from './validate';

const actions: VoiceActionSpec[] = [
  {
    name: 'setState',
    description: 'Move the item to a state',
    args: {
      type: 'object',
      properties: { item: { type: 'string' }, state: { type: 'string' } },
      required: ['state'],
    },
  },
  {
    name: 'rank',
    description: 'Move the item to the top or bottom',
    args: {
      type: 'object',
      properties: { item: { type: 'string' }, slot: { enum: ['first', 'last'] } },
      required: ['item', 'slot'],
    },
  },
];
const t = aTranscript({ text: 'set ship 906 to active' });

describe('validate', () => {
  it('drops an action the spec doesn’t list and args that fail the schema', () => {
    const unknown = { action: 'delete', args: { item: 'SHIP-906' } };
    expect(checkCall(actions, unknown, t)).toEqual({
      ok: false,
      call: unknown,
      failure: { kind: 'noValidAction', detail: { action: 'delete', reason: 'none' } },
    });
    for (const args of [{ item: 'SHIP-906' }, { state: 3 }, { state: 'Active', colour: 'red' }]) {
      const call = { action: 'setState', args };
      expect(checkCall(actions, call, t)).toEqual({
        ok: false,
        call,
        failure: { kind: 'noValidAction', detail: { action: 'setState', reason: 'invalidArgs' } },
      });
    }
    const wrongSlot = { action: 'rank', args: { item: 'SHIP-906', slot: 'middle' } };
    expect(checkCall(actions, wrongSlot, t).ok).toBe(false);
  });
  it('keeps a call whose action is listed and whose args fit', () => {
    const call = { action: 'setState', args: { item: 'SHIP-906', state: 'Active' } };
    expect(checkCall(actions, call, t)).toEqual({ ok: true, call });
    expect(
      checkCall(actions, { action: 'rank', args: { item: 'SHIP-906', slot: 'first' } }, t).ok,
    ).toBe(true);
  });
  it('checks types, arrays of items, consts and nullable types', () => {
    expect(fits([1, 2], { type: 'array', items: { type: 'integer' } })).toBe(true);
    expect(fits([1, 2.5], { type: 'array', items: { type: 'integer' } })).toBe(false);
    expect(fits(null, { type: ['string', 'null'] })).toBe(true);
    expect(fits(true, { type: ['string', 'null'] })).toBe(false);
    expect(fits('first', { const: 'first' })).toBe(true);
    expect(fits('last', { const: 'first' })).toBe(false);
  });
});
