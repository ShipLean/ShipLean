// Voice for screen specs: a fake voice state any screen can render with, or the real provider over fake engines.
import { useSyncExternalStore, type ReactElement, type ReactNode } from 'react';
import type { ActionRunner } from '../actions/types';
import type { Scheme } from '../theme/themes';
import { configureEngines, notReadyEngines } from '../voice/engines';
import type { RetryChange, VoiceApi, VoiceUiState } from '../voice/internals';
import { VoiceContext, VoiceProvider } from '../voice/VoiceProvider';
import type { FakeRunner } from './actions';
import { renderWithRunner } from './renderWithRunner';
import { FakeCommandModel, FakeMicPermission, FakeModelStore, FakeRecognizer } from './voice';

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

// useVoice() under it returns the fake's api, and follows set().
export function FakeVoiceProvider({ voice, children }: { voice: FakeVoice; children: ReactNode }) {
  const api = useSyncExternalStore(voice.subscribe, () => voice.api);
  return <VoiceContext.Provider value={api}>{children}</VoiceContext.Provider>;
}

export interface FakeEngines {
  recognizer: FakeRecognizer;
  model: FakeCommandModel;
  models: FakeModelStore;
  mic: FakeMicPermission;
}

let installed = false;

// The engines go back to not ready after every test that installed fakes.
afterEach(() => {
  if (!installed) return;
  installed = false;
  configureEngines(notReadyEngines());
});

// Configures T0r's fakes (ready, mic granted) as the engines every VoiceProvider mounted in this test reads.
export function installFakeEngines(overrides: Partial<FakeEngines> = {}): FakeEngines {
  const fakes: FakeEngines = {
    recognizer: new FakeRecognizer(),
    model: new FakeCommandModel(),
    models: new FakeModelStore(),
    mic: new FakeMicPermission(),
    ...overrides,
  };
  configureEngines(fakes);
  installed = true;
  return fakes;
}

export interface RenderWithVoiceOptions<R extends ActionRunner> {
  // A fake voice state (fakeVoice() unless engines are given).
  voice?: FakeVoice;
  // The real VoiceProvider over fake engines, with these overrides.
  engines?: Partial<FakeEngines>;
  runner?: R;
  scheme?: Scheme;
}

// renderWithRunner with voice: the real provider when engines are given, else a FakeVoiceProvider.
export async function renderWithVoice<R extends ActionRunner = FakeRunner>(
  ui: ReactElement,
  { voice, engines, runner, scheme }: RenderWithVoiceOptions<R> = {},
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
    <FakeVoiceProvider voice={fake}>{ui}</FakeVoiceProvider>,
    {
      runner,
      scheme,
    },
  );
  return Object.assign(result, { voice: fake, engines: undefined });
}
