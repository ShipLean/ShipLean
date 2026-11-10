import { describe, expect, it } from 'vitest';
import type { ScreenVoiceSpec, Transcript, VoiceActionSpec } from './contracts';
import { buildSystemPrompt, userTurn } from './commandPrompt';

const key = { type: 'string', pattern: '^[A-Z]+-[0-9]+$' };
const states = { type: 'string', enum: ['New', 'Active', 'Resolved', 'Closed', 'Removed'] };

// A busy screen: 12 actions with the argument shapes the area specs use.
const TWELVE: VoiceActionSpec[] = [
  {
    name: 'setState',
    description: 'Move an item to a state.',
    args: { type: 'object', properties: { key, state: states }, required: ['key', 'state'] },
  },
  {
    name: 'assign',
    description: 'Give an item to a person.',
    args: {
      type: 'object',
      properties: { key, person: { type: 'string' } },
      required: ['key', 'person'],
    },
  },
  {
    name: 'unassign',
    description: 'Take an item off its person.',
    args: { type: 'object', properties: { key }, required: ['key'] },
  },
  {
    name: 'addNote',
    description: 'Post a note on an item.',
    args: {
      type: 'object',
      properties: { key, text: { type: 'string' } },
      required: ['key', 'text'],
    },
    noteArg: 'text',
  },
  {
    name: 'setHealth',
    description: 'Set a Feature’s health.',
    args: {
      type: 'object',
      properties: { key, health: { type: 'string', enum: ['onTrack', 'atRisk', 'blocked'] } },
      required: ['key', 'health'],
    },
  },
  {
    name: 'setStatus',
    description: 'Replace a Feature’s status line.',
    args: {
      type: 'object',
      properties: { key, text: { type: 'string' } },
      required: ['key', 'text'],
    },
    noteArg: 'text',
  },
  {
    name: 'moveRank',
    description: 'Move an item up or down the list.',
    args: {
      type: 'object',
      properties: { key, slot: { type: 'string', enum: ['first', 'last', 'before'] }, before: key },
      required: ['key', 'slot'],
    },
  },
  {
    name: 'addStory',
    description: 'Add a Story under a Feature.',
    args: {
      type: 'object',
      properties: { parent: key, title: { type: 'string' } },
      required: ['parent', 'title'],
    },
    noteArg: 'title',
  },
  {
    name: 'approve',
    description: 'Approve decisions by number.',
    args: {
      type: 'object',
      properties: { key, numbers: { type: 'array', items: { type: 'integer' } } },
      required: ['key', 'numbers'],
    },
  },
  {
    name: 'stillTrue',
    description: 'Confirm a status line still holds.',
    args: { type: 'object', properties: { key }, required: ['key'] },
  },
  {
    name: 'open',
    description: 'Open an item.',
    args: { type: 'object', properties: { key }, required: ['key'] },
  },
  {
    name: 'filter',
    description: 'Show only some rows.',
    args: {
      type: 'object',
      properties: { chip: { type: 'string', enum: ['needsMe', 'open'] }, on: { type: 'boolean' } },
      required: ['chip'],
    },
  },
];

const transcript = (text: string): Transcript => ({ text, words: [], durationMs: 2000 });

// Characters per token for English prose mixed with JSON schemas, on the low side so the estimate errs high.
const CHARS_PER_TOKEN = 3;

describe('buildSystemPrompt', () => {
  it('lists every action with its description and the context, under the token estimate for 12 actions', () => {
    // Wave amendment 24: the prefix holds only what depends on the screen and its actions, and the context is read at
    // parse time (userTurn). So the context's part of this test is that the prefix never reads it.
    const spec: Pick<ScreenVoiceSpec, 'screen' | 'actions'> & { context(): never } = {
      screen: 'workItem',
      actions: TWELVE,
      context: () => {
        throw new Error('the prefix read the context');
      },
    };

    const prompt = buildSystemPrompt(spec);

    for (const action of TWELVE) {
      const line = prompt.split('\n').find((l) => l.startsWith(`- ${action.name}: `));
      expect(line).toBeDefined();
      expect(line).toContain(action.description);
      expect(line).toContain(JSON.stringify(action.args));
    }
    expect(prompt).toContain('noteSpan');
    expect(prompt).toContain('/no_think');
    // About 1.5k tokens at most for 12 actions, by a characters-per-token estimate (no tokenizer on the host).
    expect(prompt.length / CHARS_PER_TOKEN).toBeLessThan(1500);
  });

  it('names a noteArg action’s note argument only to say it comes back as noteSpan', () => {
    const prompt = buildSystemPrompt({ screen: 'workItem', actions: [TWELVE[3]] });

    expect(prompt).toContain('Its text is never written: give noteSpan');
  });
});

describe('userTurn', () => {
  it('carries the transcript verbatim, last', () => {
    const text = 'Set SHIP-12 to Active and add a note: the API returns 500 on retry, "twice".';
    const context = { focus: 'SHIP-12', states: ['New', 'Active'] };

    const turn = userTurn(transcript(text), context);

    expect(turn.endsWith(text)).toBe(true);
    expect(turn.indexOf(JSON.stringify(context))).toBeGreaterThanOrEqual(0);
    expect(turn.indexOf(JSON.stringify(context))).toBeLessThan(turn.indexOf(text));
  });

  it('shows the context it is given, so a context change between commands reaches the model', () => {
    const first = userTurn(transcript('open it'), { focus: 'SHIP-1' });
    const second = userTurn(transcript('open it'), { focus: 'SHIP-2' });

    expect(first).toContain('"focus":"SHIP-1"');
    expect(second).toContain('"focus":"SHIP-2"');
  });
});
