// A small screen for the exporter's own tests: two item keys, two states, a note action and a state action. It is
// built from plain data by a pure factory, as every real screen's voice spec is. The exporter never calls toActions.
import type { ScreenVoiceSpec } from '../../../src/voice/contracts';

export interface FixtureScreenData {
  keys: string[];
  states: string[];
}

export const FIXTURE_SCREEN = 'fixture';

export const fixtureData: FixtureScreenData = {
  keys: ['SHIP-12', 'SHIP-40'],
  states: ['Active', 'Done'],
};

export function fixtureVoiceSpec(data: FixtureScreenData): ScreenVoiceSpec {
  return {
    screen: FIXTURE_SCREEN,
    actions: [
      {
        name: 'comment',
        description: 'Post a note on an item.',
        args: {
          type: 'object',
          properties: { key: { type: 'string', enum: data.keys }, body: { type: 'string' } },
          required: ['key', 'body'],
        },
        noteArg: 'body',
      },
      {
        name: 'setState',
        description: 'Move an item to another state.',
        args: {
          type: 'object',
          properties: {
            key: { type: 'string', enum: data.keys },
            state: { type: 'string', enum: data.states },
          },
          required: ['key', 'state'],
        },
      },
    ],
    context: () => ({ items: data.keys, states: data.states }),
    hotwords: () => [...data.keys, ...data.states],
    toActions: (_calls, transcript) => ({
      ok: false,
      failure: { kind: 'unclear', detail: { heard: transcript.text } },
    }),
  };
}
