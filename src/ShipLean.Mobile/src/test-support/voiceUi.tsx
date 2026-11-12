// Voice for screen specs: a fake voice state any screen can render with, or the real provider over fake engines.
import { useState, useSyncExternalStore, type ReactElement, type ReactNode } from 'react';
import type { ActionRunner } from '../actions/types';
import type { Scheme } from '../theme/themes';
import { configureEngines, notReadyEngines } from '../voice/engines';
import {
  VoiceInternalsContext,
  type RetryChange,
  type VoiceApi,
  type VoiceInternals,
  type VoiceUiState,
} from '../voice/internals';
import { clearComposerRequest } from '../voice/typed/composerRequest';
import { VoiceContext, VoiceProvider } from '../voice/VoiceProvider';
import type { FakeRunner } from './actions';
import { renderWithRunner } from './renderWithRunner';
import { fakeInternals } from './voiceInternals';
import {
  FakeCommandModel,
  FakeMicPermission,
  FakeModelStore,
  FakeRecognizer,
  fakeAudioInputs,
  fakeTuningStore,
} from './voice';
import type { AudioInputs, MicTuningStore } from '../voice/contracts';

export interface FakeVoice {
  // The current api: state is the last set (idle with the mic granted unless given).
  readonly api: VoiceApi;
  // Every call, in order: 'holdStart', 'apply(["l1"])', 'submitTyped("move it")', …
  readonly calls: string[];
  set(state: VoiceUiState): void;
  subscribe(cb: () => void): () => void;
}

export function fakeVoice(
  state: VoiceUiState = { kind: 'idle', permission: 'granted' },
): FakeVoice {
  const calls: string[] = [];
  const listeners = new Set<() => void>();
  const record =
    (name: string) =>
    (...args: unknown[]) => {
      calls.push(
        args.length === 0 ? name : `${name}(${args.map((a) => JSON.stringify(a)).join(', ')})`,
      );
    };
  const methods = {
    holdStart: record('holdStart'),
    holdEnd: record('holdEnd'),
    cancel: record('cancel'),
    apply: (lineIds?: string[]) =>
      lineIds === undefined ? record('apply')() : record('apply')(lineIds),
    fix: record('fix'),
    typeInstead: record('typeInstead'),
    submitTyped: (text: string) => record('submitTyped')(text),
    retryWith: (change: RetryChange) => record('retryWith')(change),
    dismiss: record('dismiss'),
  };
  let api: VoiceApi = { ...methods, state };
  return {
    get api() {
      return api;
    },
    calls,
    set(next) {
      api = { ...methods, state: next };
      for (const cb of [...listeners]) cb();
    },
    subscribe(cb) {
      listeners.add(cb);
      return () => {
        listeners.delete(cb);
      };
    },
  };
}

// useVoice() under it returns the fake's api, and follows set(); useVoiceInternals() returns the internals (a
// fakeInternals() unless given), as it does under the real provider.
export function FakeVoiceProvider({
  voice,
  internals,
  children,
}: {
  voice: FakeVoice;
  internals?: VoiceInternals;
  children: ReactNode;
}) {
  const api = useSyncExternalStore(voice.subscribe, () => voice.api);
  const [fallback] = useState(() => fakeInternals());
  return (
    <VoiceContext.Provider value={api}>
      <VoiceInternalsContext.Provider value={internals ?? fallback}>
        {children}
      </VoiceInternalsContext.Provider>
    </VoiceContext.Provider>
  );
}

export interface FakeEngines {
  recognizer: FakeRecognizer;
  model: FakeCommandModel;
  models: FakeModelStore;
  mic: FakeMicPermission;
  inputs: AudioInputs;
  tuning: MicTuningStore;
}

// After every test the engines go back to not ready (also when a test called configureEngines itself) and no
// composer request is left pending. Import this file at the top of a spec: Jest refuses a hook registered from a
// require inside a test body.
afterEach(() => {
  configureEngines(notReadyEngines());
  clearComposerRequest();
});

// Configures T0r's fakes (ready, mic granted) as the engines every VoiceProvider mounted in this test reads.
export function installFakeEngines(overrides: Partial<FakeEngines> = {}): FakeEngines {
  const fakes: FakeEngines = {
    recognizer: new FakeRecognizer(),
    model: new FakeCommandModel(),
    models: new FakeModelStore(),
    mic: new FakeMicPermission(),
    inputs: fakeAudioInputs(),
    tuning: fakeTuningStore(),
    ...overrides,
  };
  configureEngines(fakes);
  return fakes;
}

export interface RenderWithVoiceOptions<R extends ActionRunner> {
  // A fake voice state (fakeVoice() unless engines are given).
  voice?: FakeVoice;
  // The real VoiceProvider over fake engines, with these overrides.
  engines?: Partial<FakeEngines>;
  // The internals the fake voice path provides (fakeInternals() unless given), e.g. to register a screen spec.
  internals?: VoiceInternals;
  runner?: R;
  scheme?: Scheme;
}

// renderWithRunner with voice: the real provider when engines are given, else a FakeVoiceProvider.
export async function renderWithVoice<R extends ActionRunner = FakeRunner>(
  ui: ReactElement,
  { voice, engines, internals, runner, scheme }: RenderWithVoiceOptions<R> = {},
) {
  if (engines !== undefined) {
    const fakes = installFakeEngines(engines);
    const result = await renderWithRunner<R>(<VoiceProvider>{ui}</VoiceProvider>, {
      runner,
      scheme,
    });
    return Object.assign(result, { engines: fakes, voice: undefined });
  }
  const fake = voice ?? fakeVoice();
  const result = await renderWithRunner<R>(
    <FakeVoiceProvider voice={fake} internals={internals}>
      {ui}
    </FakeVoiceProvider>,
    {
      runner,
      scheme,
    },
  );
  return Object.assign(result, { voice: fake, engines: undefined });
}
