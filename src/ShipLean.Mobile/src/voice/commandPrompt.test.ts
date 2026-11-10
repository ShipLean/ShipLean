import { describe, expect, it } from 'vitest';
import { buildGrammar, parseModelOutput } from './commandPrompt';
import type { VoiceActionSpec } from './contracts';

const actions: VoiceActionSpec[] = [
  {
    name: 'setState',
    description: 'Move the item to a state',
    args: {
      type: 'object',
      properties: {
        state: { type: 'string', enum: ['Active', 'Done'] },
        key: { type: 'string' },
      },
      required: ['state'],
    },
  },
  {
    name: 'addNote',
    description: 'Leave a note on the item',
    args: { type: 'object', properties: { text: { type: 'string' } }, required: ['text'] },
    noteArg: 'text',
  },
];

function rules(grammar: string): Map<string, string> {
  return new Map(
    grammar.split('\n').map((line) => {
      const [name, body] = line.split(' ::= ');
      return [name, body];
    }),
  );
}

const q = (json: string) => `"${json.replace(/"/g, '\\"')}"`;

describe('buildGrammar', () => {
  it('buildGrammar allows only the spec action names and each action argument enums', () => {
    const r = rules(buildGrammar({ actions }));

    expect(r.get('root')).toBe('ws "[" ws ( call ( ws "," ws call )* )? ws "]" ws');
    expect(r.get('call')).toBe('call-0 | call-1');
    expect(r.get('call-0')).toBe(
      `"{" ws ${q('"action"')} ws ":" ws ${q('"setState"')} ws "," ws ${q('"args"')} ws ":" ws ` +
        `"{" ws ${q('"state"')} ws ":" ws ( ${q('"Active"')} | ${q('"Done"')} ) ` +
        `( ws "," ws ${q('"key"')} ws ":" ws string )? ws "}" ws "}"`,
    );
    expect(r.get('call-1')).toContain(`ws ":" ws ${q('"addNote"')} ws`);
    expect([...r.keys()].filter((k) => k.startsWith('call-'))).toEqual(['call-0', 'call-1']);
  });

  it('allows only [] for a screen with no actions', () => {
    expect(rules(buildGrammar({ actions: [] })).get('root')).toBe('ws "[" ws "]" ws');
  });

  it('buildGrammar gives a noteArg action a noteSpan of two integers and no note text', () => {
    const r = rules(buildGrammar({ actions }));

    expect(r.get('call-1')).toBe(
      `"{" ws ${q('"action"')} ws ":" ws ${q('"addNote"')} ws "," ws ${q('"args"')} ws ":" ws "{" ws "}" ` +
        `ws "," ws ${q('"noteSpan"')} ws ":" ws notespan ws "}"`,
    );
    expect(r.get('notespan')).toBe(
      `"{" ws ${q('"start"')} ws ":" ws natural ws "," ws ${q('"end"')} ws ":" ws natural ws "}"`,
    );
    expect(r.get('natural')).toBe('"0" | [1-9] [0-9]{0,9}');
    expect(r.get('call-0')).not.toContain('noteSpan');
  });
});

describe('parseModelOutput', () => {
  it('parseModelOutput parses calls with noteSpan and returns [] for text outside the grammar', () => {
    expect(
      parseModelOutput(
        '[{"action":"setState","args":{"state":"Active"}},' +
          '{"action":"addNote","args":{},"noteSpan":{"start":12,"end":40}}]',
      ),
    ).toEqual([
      { action: 'setState', args: { state: 'Active' } },
      { action: 'addNote', args: {}, noteSpan: { start: 12, end: 40 } },
    ]);
    expect(parseModelOutput(' [] ')).toEqual([]);

    expect(parseModelOutput('Sure! [{"action":"setState","args":{}}]')).toEqual([]);
    expect(parseModelOutput('{"action":"setState","args":{}}')).toEqual([]);
    expect(parseModelOutput('[{"action":"setState","args":{}}')).toEqual([]);
    expect(parseModelOutput('[{"action":"setState"}]')).toEqual([]);
    expect(parseModelOutput('[{"action":"addNote","args":{},"note":"the API"}]')).toEqual([]);
    expect(
      parseModelOutput('[{"action":"addNote","args":{},"noteSpan":{"start":1.5,"end":4}}]'),
    ).toEqual([]);
    expect(
      parseModelOutput(
        '[{"action":"setState","args":{}},{"action":"addNote","args":{},"noteSpan":[1,4]}]',
      ),
    ).toEqual([]);
  });
});
