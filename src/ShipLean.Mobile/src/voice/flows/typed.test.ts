import { describe, expect, it } from 'vitest';
import { FakeRecognizer } from '../../test-support/voice';
import { flowHarness } from '../../test-support/voiceFlows';
import { submitTyped } from './typed';

describe('typed', () => {
  it('submitTyped skips the recognizer and marks the preview typed', async () => {
    const { v, parsed } = flowHarness(
      { key: 'SHIP-906', state: 'New', revision: 4, decisions: ['D10'] },
      (text) => (text === 'approve D10' ? [{ action: 'approve', args: { number: 'D10' } }] : []),
    );

    await submitTyped(v, 'approve D10');

    expect((v.engines().recognizer as FakeRecognizer).starts).toEqual([]);
    expect(parsed).toEqual(['approve D10']);
    expect(v.resolved).toEqual([
      { transcript: { text: 'approve D10', words: [], durationMs: 0 }, source: 'typed' },
    ]);
    expect(v.state()).toMatchObject({
      kind: 'preview',
      source: 'typed',
      heard: 'approve D10',
      lines: [{ id: 'approve-D10', text: 'Approve D10' }],
    });
  });

  it('submitTyped ignores blank text', async () => {
    const { v } = flowHarness(
      { key: 'SHIP-906', state: 'New', revision: 4, decisions: [] },
      () => [],
    );

    await submitTyped(v, '   ');

    expect(v.resolved).toEqual([]);
  });
});
