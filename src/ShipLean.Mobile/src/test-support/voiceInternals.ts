// A fake of the voice provider's internals for the flow tests. Pure (no React Native), so vitest can use it.
import type { VoiceEngines, Transcript } from '../voice/contracts';
import type { VoiceInternals, VoiceSource, VoiceUiState } from '../voice/internals';
import { fakeRunner } from './actions';
import { FakeCommandModel, FakeMicPermission, FakeModelStore, FakeRecognizer } from './voice';

export interface FakeInternals extends VoiceInternals {
  // Every state set, in order; state() is the last one (idle with the mic granted before any).
  readonly sets: VoiceUiState[];
  // The lineIds of every dispatch, in order.
  readonly dispatched: (string[] | undefined)[];
  readonly resolved: { transcript: Transcript; source: VoiceSource }[];
}

function fakeEngines(): VoiceEngines {
  return {
    recognizer: new FakeRecognizer(),
    model: new FakeCommandModel(),
    models: new FakeModelStore(),
    mic: new FakeMicPermission(),
  };
}

// Records set, dispatch and resolve. The spec, last parse, engines and runner can be given; dispatch returns
// batch-1, batch-2 and so on.
export function fakeInternals(overrides: Partial<VoiceInternals> = {}): FakeInternals {
  const sets: VoiceUiState[] = [];
  const dispatched: (string[] | undefined)[] = [];
  const resolved: { transcript: Transcript; source: VoiceSource }[] = [];
  const engines = fakeEngines();
  const fake: FakeInternals = {
    sets,
    dispatched,
    resolved,
    state: () => sets.at(-1) ?? { kind: 'idle', permission: 'granted' },
    set: (s) => {
      sets.push(s);
    },
    spec: () => null,
    engines: () => engines,
    runner: fakeRunner(),
    last: () => null,
    resolve: async (transcript, { source }) => {
      resolved.push({ transcript, source });
    },
    resolveCalls: () => ({
      ok: false,
      failure: { kind: 'noValidAction', detail: { action: null, reason: 'none' } },
    }),
    dispatch: (lineIds) => {
      dispatched.push(lineIds);
      return `batch-${dispatched.length}`;
    },
  };
  return { ...fake, ...overrides, sets, dispatched, resolved };
}
