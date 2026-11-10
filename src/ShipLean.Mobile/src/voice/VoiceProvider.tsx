// N9: the voice provider. It reads the configured engines and the session's runner, keeps the voice state and the
// focused screen's spec, and routes Fix, typed commands, corrections and conflicts to src/voice/flows.
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  useSyncExternalStore,
  type ReactNode,
} from 'react';
import { NavigationContext } from 'expo-router/react-navigation';
import { useActionRunner } from '../actions/ActionRunnerProvider';
import { UNDO } from '../actions/undo';
import type {
  ModelStatus,
  ParsedCall,
  ResolveResult,
  ScreenVoiceSpec,
  Transcript,
  VoiceEngines,
  ViewIntent,
} from './contracts';
import { engines } from './engines';
import { discardApply, queueApply, retryApply } from './flows/applyFailed';
import { onConflict } from './flows/conflict';
import { startFix } from './flows/fix';
import { retryWith } from './flows/retryWith';
import { submitTyped } from './flows/typed';
import {
  VoiceInternalsContext,
  useVoiceInternals,
  type MicStatus,
  type VoiceApi,
  type VoiceInternals,
  type VoiceUiState,
} from './internals';
import { resolveCommand } from './resolve';
import { AnswerSheet } from './sheets/AnswerSheet';
import { ApplyFailedSheet } from './sheets/ApplyFailedSheet';
import { ListeningSheet } from './sheets/ListeningSheet';
import { PreviewSheet } from './sheets/PreviewSheet';
import { VoiceFailureSheet } from './sheets/VoiceFailureSheet';

export type {
  DisabledReason,
  MicStatus,
  RetryChange,
  VoiceApi,
  VoiceSource,
  VoiceUiState,
} from './internals';

// A screen's handler for the view intents its own spec returns (filter, sort, open a section).
export type ScreenViewHandler = (view: Extract<ViewIntent, { kind: 'screen' }>) => void;

export interface ScreenVoiceOptions {
  onView?: ScreenViewHandler;
}

const noop = () => {};

// Outside a provider there is no voice: disabled with no models, and every call does nothing.
const NO_VOICE: VoiceApi = {
  state: { kind: 'disabled', reason: 'noModels', permission: 'undetermined' },
  holdStart: noop,
  holdEnd: noop,
  cancel: noop,
  apply: noop,
  fix: noop,
  typeInstead: noop,
  submitTyped: noop,
  retryWith: noop,
  dismiss: noop,
};

// The value useVoice() returns. VoiceProvider provides it; tests may provide a fake one.
export const VoiceContext = createContext<VoiceApi>(NO_VOICE);

interface ScreenEntry {
  spec: ScreenVoiceSpec;
  onView: ScreenViewHandler | undefined;
}

type Register = (entry: ScreenEntry) => () => void;

const RegisterContext = createContext<Register | null>(null);
const EnginesContext = createContext<VoiceEngines | null>(null);

// The resting state from the models and the mic: idle once the model is ready and the mic isn't refused.
export function restingState(status: ModelStatus, permission: MicStatus): VoiceUiState {
  switch (status.phase) {
    case 'ready':
      return permission === 'deniedCanAsk' || permission === 'deniedFinal'
        ? { kind: 'disabled', reason: 'micOff', permission }
        : { kind: 'idle', permission };
    case 'downloading':
    case 'paused':
    case 'checking':
      return { kind: 'disabled', reason: 'downloading', permission };
    case 'skipped':
      return { kind: 'disabled', reason: 'skipped', permission };
    case 'none':
    case 'failed':
      return { kind: 'disabled', reason: 'noModels', permission };
  }
}

// The provider's mutable state, outside React so the internals read it at call time.
class VoiceStore {
  // A state the flows set; null rests on the models and the mic.
  machine: VoiceUiState | null = null;
  permission: MicStatus = 'undetermined';
  last: { calls: ParsedCall[]; transcript: Transcript; result: ResolveResult } | null = null;
  private screens: ScreenEntry[] = [];
  private version = 0;
  private readonly listeners = new Set<() => void>();

  readonly subscribe = (cb: () => void) => {
    this.listeners.add(cb);
    return () => {
      this.listeners.delete(cb);
    };
  };

  readonly snapshot = () => this.version;

  setMachine(s: VoiceUiState): void {
    this.machine = s.kind === 'idle' || s.kind === 'disabled' ? null : s;
    this.changed();
  }

  setPermission(p: MicStatus): void {
    this.permission = p;
    this.changed();
  }

  // The newest registration is the focused screen; unregistering hands back to the one before.
  readonly register: Register = (entry) => {
    this.screens = [...this.screens, entry];
    return () => {
      this.screens = this.screens.filter((e) => e !== entry);
    };
  };

  spec(): ScreenVoiceSpec | null {
    return this.screens.at(-1)?.spec ?? null;
  }

  private changed(): void {
    this.version++;
    for (const cb of [...this.listeners]) cb();
  }
}

function stateOf(store: VoiceStore, status: ModelStatus): VoiceUiState {
  return store.machine ?? restingState(status, store.permission);
}

function useStatusOf(e: VoiceEngines): ModelStatus {
  const subscribe = useCallback((cb: () => void) => e.models.subscribe(cb), [e]);
  const status = useCallback(() => e.models.status(), [e]);
  return useSyncExternalStore(subscribe, status);
}

export function VoiceProvider({ children }: { children: ReactNode }) {
  const runner = useActionRunner();
  const [e] = useState(engines);
  const [store] = useState(() => new VoiceStore());
  useSyncExternalStore(store.subscribe, store.snapshot);
  const status = useStatusOf(e);

  // TODO: ask the mic again when the app comes back to the front, so a change in Settings shows.
  useEffect(() => {
    let live = true;
    void e.mic.status().then((p) => {
      if (live) store.setPermission(p);
    });
    return () => {
      live = false;
    };
  }, [e, store]);

  const internals = useMemo<VoiceInternals>(
    () => ({
      state: () => stateOf(store, e.models.status()),
      set: (s) => store.setMachine(s),
      spec: () => store.spec(),
      engines: () => e,
      runner,
      last: () => store.last,
      // TODO: listening, thinking and the preview: parse the transcript with the focused screen's prepared spec,
      // keep it as last() and set the preview, answer or failure.
      resolve: async (t, opts) => {
        void t;
        void opts;
      },
      resolveCalls: (calls, t) => {
        const spec = store.spec();
        if (spec === null)
          return {
            ok: false,
            failure: { kind: 'noValidAction', detail: { action: null, reason: 'none' } },
          };
        return resolveCommand(spec, calls, t);
      },
      // TODO: the parse's source picks the origin; the preview's removed lines are left out by the machine.
      dispatch: (lineIds) => {
        const last = store.last;
        if (last === null || !last.result.ok) throw new Error('There is no preview to apply.');
        const { actions, lines } = last.result;
        const chosen =
          lineIds === undefined
            ? actions
            : actions.filter((_, i) => lineIds.includes(lines[i]?.id ?? ''));
        return runner.dispatch(chosen, { undo: UNDO.voice, origin: 'voice', allOrNothing: true });
      },
    }),
    [store, e, runner],
  );

  // A 409 on a voice batch rebuilds its preview.
  useEffect(
    () => runner.subscribeConflicts((batchId, lines) => onConflict(internals, batchId, lines)),
    [runner, internals],
  );
  // TODO: a failed voice batch goes to onApplyFailed once the runner reports failed batches.

  const state = stateOf(store, status);
  const api = useMemo<VoiceApi>(
    () => ({
      state,
      // TODO: hold to talk: start the recognizer with the focused screen's hotwords, and stop or cancel it.
      holdStart: noop,
      holdEnd: noop,
      cancel: () => store.setMachine(restingState(e.models.status(), store.permission)),
      // TODO: dispatch the preview's kept lines and show applying, then the undo toast.
      apply: (lineIds) => {
        void lineIds;
      },
      fix: () => startFix(internals),
      // TODO: open the dock's Command composer with the heard text.
      typeInstead: noop,
      submitTyped: (text) => void submitTyped(internals, text),
      retryWith: (change) => void retryWith(internals, change),
      dismiss: () => store.setMachine(restingState(e.models.status(), store.permission)),
    }),
    [state, store, e, internals],
  );

  return (
    <EnginesContext.Provider value={e}>
      <VoiceInternalsContext.Provider value={internals}>
        <RegisterContext.Provider value={store.register}>
          <VoiceContext.Provider value={api}>{children}</VoiceContext.Provider>
        </RegisterContext.Provider>
      </VoiceInternalsContext.Provider>
    </EnginesContext.Provider>
  );
}

export function useVoice(): VoiceApi {
  return useContext(VoiceContext);
}

// Registers the screen's spec and its view handler while the screen is focused; the newest registration wins.
// It registers once per mount and keeps the spec and onView current through a stable entry, so a re-render never
// moves a screen up the stack. Inside a navigator it follows focus and blur (tabs stay mounted, so a background tab
// must not hold the mic); NavigationContext is undefined outside one, and then mount and unmount decide.
export function useScreenVoice(spec: ScreenVoiceSpec, opts?: ScreenVoiceOptions): void {
  const register = useContext(RegisterContext);
  const nav = useContext(NavigationContext);
  const onView = opts?.onView;
  const entry = useRef<ScreenEntry>({ spec, onView });
  useEffect(() => {
    entry.current.spec = spec;
    entry.current.onView = onView;
  });
  useEffect(() => {
    if (register === null) return;
    const mine = entry.current;
    if (nav === undefined) return register(mine);
    let drop: (() => void) | null = null;
    const take = () => {
      drop ??= register(mine);
    };
    const give = () => {
      drop?.();
      drop = null;
    };
    if (nav.isFocused()) take();
    const offFocus = nav.addListener('focus', take);
    const offBlur = nav.addListener('blur', give);
    return () => {
      offFocus();
      offBlur();
      give();
    };
  }, [register, nav]);
}

// The model status from the provider's engines, or the configured ones outside a provider.
export function useModelStatus(): ModelStatus {
  const inProvider = useContext(EnginesContext);
  const [configured] = useState(engines);
  return useStatusOf(inProvider ?? configured);
}

// The sheets for the voice state, over the screen.
export function VoiceSheetsHost() {
  const voice = useVoice();
  const internals = useVoiceInternals();
  const { state } = voice;
  switch (state.kind) {
    case 'listening':
      return <ListeningSheet state={state} />;
    case 'preview':
      return (
        <PreviewSheet
          state={state}
          onApply={voice.apply}
          onFix={voice.fix}
          onCancel={voice.cancel}
        />
      );
    case 'failure':
      return (
        <VoiceFailureSheet
          state={state}
          spec={internals.spec()}
          onRetry={voice.retryWith}
          onTypeInstead={voice.typeInstead}
          onClose={voice.dismiss}
        />
      );
    case 'answer':
      return <AnswerSheet content={state.content} onClose={voice.dismiss} />;
    case 'applyFailed':
      return (
        <ApplyFailedSheet
          state={state}
          onRetry={() => retryApply(internals)}
          onQueue={() => queueApply(internals)}
          onDiscard={() => discardApply(internals)}
        />
      );
    case 'idle':
    case 'disabled':
    case 'thinking':
    case 'applying':
      return null;
  }
}
