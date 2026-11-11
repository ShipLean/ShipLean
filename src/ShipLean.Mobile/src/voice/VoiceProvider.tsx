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
import { AppState } from 'react-native';
import { router } from 'expo-router';
import { NavigationContext } from 'expo-router/react-navigation';
import { useActionRunner } from '../actions/ActionRunnerProvider';
import { systemNow } from '../actions/clock';
import type { Pending } from '../actions/types';
import { UNDO } from '../actions/undo';
import type {
  ModelStatus,
  ParsedCall,
  ResolveResult,
  ScreenVoiceSpec,
  Transcript,
  VoiceEngines,
  VoiceFailure,
  ViewIntent,
} from './contracts';
import { engines } from './engines';
import { resetSilentHolds } from './failures/silentHolds';
import { discardApply, onApplyFailed, queueApply, retryApply } from './flows/applyFailed';
import { onConflict } from './flows/conflict';
import { fixHeard, startFix } from './flows/fix';
import { retryWith } from './flows/retryWith';
import { submitTyped } from './flows/typed';
import {
  VoiceInternalsContext,
  useVoiceInternals,
  type MicStatus,
  type VoiceApi,
  type VoiceInternals,
  type VoiceSource,
  type VoiceUiState,
  type VoiceUiStateOf,
} from './internals';
import {
  isYes,
  keptActions,
  keptLineIds,
  PreparedCache,
  restingState,
  startHold,
  turnOf,
  type Hold,
} from './machine';
import { resolveCommand } from './resolve';
import { AnswerSheet } from './sheets/AnswerSheet';
import { ApplyFailedSheet } from './sheets/ApplyFailedSheet';
import { ListeningSheet } from './sheets/ListeningSheet';
import { PreviewSheet } from './sheets/PreviewSheet';
import { VoiceFailureSheet } from './sheets/VoiceFailureSheet';
import { takeTypeInstead } from './micTuning/returnIntent';
import { requestTypeInstead } from './typed/composerRequest';

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

// States in which a press of the mic starts nothing.
const BUSY: VoiceUiState['kind'][] = ['listening', 'thinking', 'applying', 'applyFailed'];

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
// Called by a registered screen each time it renders with its spec, so watchSpec's subscribers read the new data.
const SpecRenderedContext = createContext<(() => void) | null>(null);
const EnginesContext = createContext<VoiceEngines | null>(null);

export { restingState } from './machine';

interface HeldMic {
  hold: Hold;
  // The preview that was open when the mic was pressed: "yes" applies it, and a hold while fixing goes to Fix.
  prior: VoiceUiStateOf<'preview'> | null;
}

// The provider's mutable state, outside React so the internals read it at call time.
class VoiceStore {
  // A state the machine or the flows set; null rests on the models and the mic.
  machine: VoiceUiState | null = null;
  permission: MicStatus = 'undetermined';
  last: { calls: ParsedCall[]; transcript: Transcript; result: ResolveResult } | null = null;
  // Where the last resolved command came from; it picks the batch's origin and undo window.
  source: VoiceSource = 'voice';
  held: HeldMic | null = null;
  // Bumped by every new hold, resolve, apply, cancel and dismiss, so a late answer for an older one is dropped.
  turn = 0;
  readonly cache = new PreparedCache();
  // Batches this provider dispatched (true once seen in pending()), and those already handed to onApplyFailed.
  private readonly ours = new Map<string, boolean>();
  readonly reported = new Set<string>();
  private screens: ScreenEntry[] = [];
  private version = 0;
  private readonly listeners = new Set<() => void>();
  private readonly specListeners = new Set<() => void>();

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

  // Starts a new turn and returns it.
  nextTurn(): number {
    return ++this.turn;
  }

  setHeld(h: HeldMic | null): void {
    this.held = h;
  }

  remember(last: NonNullable<VoiceStore['last']>, source: VoiceSource): void {
    this.last = last;
    this.source = source;
  }

  own(batchId: string): void {
    this.ours.set(batchId, false);
  }

  // The parts still in pending() of each batch this provider sent. A batch whose parts have all left pending() is
  // forgotten, so the sets don't grow for the whole session.
  sent(all: Pending[]): { batchId: string; parts: Pending[] }[] {
    const out: { batchId: string; parts: Pending[] }[] = [];
    for (const [batchId, seen] of this.ours) {
      const parts = all.filter((p) => p.batchId === batchId);
      if (parts.length > 0) {
        this.ours.set(batchId, true);
        out.push({ batchId, parts });
      } else if (seen) {
        this.ours.delete(batchId);
        this.reported.delete(batchId);
      }
    }
    return out;
  }

  // The newest registration is the focused screen; unregistering hands back to the one before.
  readonly register: Register = (entry) => {
    this.screens = [...this.screens, entry];
    this.specRendered();
    return () => {
      this.screens = this.screens.filter((e) => e !== entry);
    };
  };

  readonly watchSpec = (cb: () => void) => {
    this.specListeners.add(cb);
    return () => {
      this.specListeners.delete(cb);
    };
  };

  readonly specRendered = () => {
    for (const cb of [...this.specListeners]) cb();
  };

  focused(): ScreenEntry | null {
    return this.screens.at(-1) ?? null;
  }

  spec(): ScreenVoiceSpec | null {
    return this.focused()?.spec ?? null;
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

// A part the runner refused (not a conflict, which rebuilds the preview) or couldn't confirm.
function failedPart(p: Pending): boolean {
  return (p.state === 'failed' && p.failure !== 'conflict') || p.state === 'uncertain';
}

export function VoiceProvider({ children }: { children: ReactNode }) {
  const runner = useActionRunner();
  const [e] = useState(engines);
  const [store] = useState(() => new VoiceStore());
  useSyncExternalStore(store.subscribe, store.snapshot);
  const status = useStatusOf(e);

  // The mic's permission, read on mount, again when the app comes back to the front (so a change in Settings
  // shows), and before each hold.
  const readMic = useCallback(() => e.mic.status(), [e]);
  useEffect(() => {
    let live = true;
    // A failed read keeps the last known permission.
    const read = () =>
      void readMic().then(
        (p) => {
          if (live) store.setPermission(p);
        },
        () => {},
      );
    read();
    const sub = AppState.addEventListener('change', (s) => {
      if (s === 'active') read();
    });
    return () => {
      live = false;
      sub.remove();
    };
  }, [readMic, store]);

  const internals = useMemo<VoiceInternals>(() => {
    const rest = () => store.setMachine(restingState(e.models.status(), store.permission));
    const self: VoiceInternals = {
      state: () => stateOf(store, e.models.status()),
      set: (s) => store.setMachine(s),
      spec: () => store.spec(),
      watchSpec: store.watchSpec,
      engines: () => e,
      runner,
      last: () => store.last,
      // Parse with the focused screen's prepared spec, keep it as last(), and show the preview, view or failure.
      resolve: async (t, { source }) => {
        const turn = store.nextTurn();
        const spec = store.spec();
        if (spec === null) {
          store.setMachine({
            kind: 'failure',
            failure: { kind: 'noValidAction', detail: { action: null, reason: 'none' } },
            heard: t.text,
            source,
          });
          return;
        }
        store.setMachine({ kind: 'thinking', heard: t.text, source });
        let calls: ParsedCall[];
        try {
          const prepared = await store.cache.get(e.model, spec);
          // The spec goes too: the model reads the screen's context as it is now, not as it was when prepared (amendment 25).
          calls = await e.model.parse(prepared, t, spec);
        } catch {
          // A model that fails to prepare or parse leaves a way forward, never a stuck 'thinking'.
          if (store.turn === turn)
            store.setMachine({
              kind: 'failure',
              failure: { kind: 'unclear', detail: { heard: t.text } },
              heard: t.text,
              source,
            });
          return;
        }
        if (store.turn !== turn) return;
        const result = resolveCommand(spec, calls, t);
        store.remember({ calls, transcript: t, result }, source);
        const shown = turnOf(spec, result, t.text, source);
        // A hold that was understood starts the silent-hold count again.
        if (
          source === 'voice' &&
          (shown.kind === 'preview' || (shown.kind === 'view' && shown.view.kind === 'answer'))
        )
          resetSilentHolds();
        switch (shown.kind) {
          case 'preview':
            store.setMachine(shown.state);
            return;
          case 'failure':
            store.setMachine({ kind: 'failure', failure: shown.failure, heard: t.text, source });
            return;
          case 'view':
            switch (shown.view.kind) {
              case 'answer':
                store.setMachine({ kind: 'answer', content: shown.view.content });
                return;
              case 'navigate':
                rest();
                router.push(shown.view.link);
                return;
              case 'screen':
                rest();
                store.focused()?.onView?.(shown.view);
                return;
            }
        }
      },
      // Resolves calls without the model (Fix, a conflict) and keeps the result as last(), so Apply and "yes" send the
      // rebuilt actions.
      resolveCalls: (calls, t) => {
        const spec = store.spec();
        const result: ResolveResult =
          spec === null
            ? {
                ok: false,
                failure: { kind: 'noValidAction', detail: { action: null, reason: 'none' } },
              }
            : resolveCommand(spec, calls, t);
        store.remember({ calls, transcript: t, result }, store.source);
        return result;
      },
      // Each kept line's own action (lines[i] ↔ actions[i]) from the newest resolved result, sent now with the
      // toast's undo for its source.
      dispatch: (lineIds) => {
        const last = store.last;
        if (last === null || !last.result.ok) throw new Error('There is no preview to apply.');
        const typed = store.source === 'typed';
        const batchId = runner.dispatch(keptActions(last.result, lineIds), {
          undo: typed ? UNDO.typed : UNDO.voice,
          origin: store.source,
          allOrNothing: true,
          via: 'voice',
        });
        store.own(batchId);
        return batchId;
      },
    };
    return self;
  }, [store, e, runner]);

  // A 409 on a voice batch rebuilds its preview. A rebuild still waiting for the screen to read the item again is
  // cancelled with the provider, so no timer or spec watcher outlives it.
  useEffect(() => {
    const waits = new Set<() => void>();
    const stop = runner.subscribeConflicts((batchId, lines) =>
      onConflict(internals, batchId, lines, waits),
    );
    return () => {
      stop();
      for (const cancel of [...waits]) cancel();
    };
  }, [runner, internals]);

  // A batch this provider sent with a refused or unconfirmed part goes to onApplyFailed, once. Otherwise applying
  // ends once nothing of the batch is still sending; the runner's toast offers Undo from there.
  const settle = useCallback(() => {
    const all = runner.pending();
    for (const { batchId, parts } of store.sent(all)) {
      if (store.reported.has(batchId)) continue;
      if (parts.some(failedPart)) {
        store.reported.add(batchId);
        onApplyFailed(internals, batchId, parts);
      }
    }
    const s = store.machine;
    if (s?.kind !== 'applying' || store.reported.has(s.batchId)) return;
    // A refused conflict is the conflict flow's: it keeps applying until the screen holds the server's item, then
    // brings the preview back.
    if (
      all.some(
        (p) => p.batchId === s.batchId && (p.state === 'sending' || p.failure === 'conflict'),
      )
    )
      return;
    store.setMachine(restingState(e.models.status(), store.permission));
  }, [runner, store, internals, e]);
  useEffect(() => runner.subscribe(settle), [runner, settle]);

  const state = stateOf(store, status);
  const api = useMemo<VoiceApi>(() => {
    const rest = () => store.setMachine(restingState(e.models.status(), store.permission));
    const apply = (lineIds?: string[]) => {
      const s = store.machine;
      if (s?.kind !== 'preview') return;
      const ids = lineIds ?? keptLineIds(s);
      if (ids.length === 0) return;
      store.nextTurn();
      const batchId = internals.dispatch(ids);
      store.setMachine({ kind: 'applying', batchId });
      settle();
    };
    const dropHold = () => {
      store.held?.hold.cancel();
      store.setHeld(null);
    };
    return {
      state,
      // Hold to talk: listen with the focused screen's hotwords. Recorded PCM stays in memory for recognition only;
      // never write, keep or upload it.
      holdStart: () => {
        const spec = store.spec();
        const prior = store.machine?.kind === 'preview' ? store.machine : null;
        if (spec === null || store.held !== null) return;
        if (store.machine !== null && BUSY.includes(store.machine.kind)) return;
        if (restingState(e.models.status(), store.permission).kind !== 'idle') return;
        store.nextTurn();
        const hotwords = spec.hotwords();
        const listening = (partial: string): VoiceUiState => ({
          kind: 'listening',
          partial,
          screen: spec.screen,
          hotwords,
        });
        const hold = startHold(
          e.recognizer,
          hotwords,
          () => systemNow().getTime(),
          (partial) => {
            if (store.held?.hold === hold) store.setMachine(listening(partial));
          },
        );
        const held: HeldMic = { hold, prior };
        store.setHeld(held);
        store.setMachine(listening(''));
        // The mic may have been refused since the last read: drop the hold and show why. A read that fails is
        // treated as refused for this hold; the last known permission stays.
        void readMic().then(
          (p) => {
            store.setPermission(p);
            if ((p === 'deniedCanAsk' || p === 'deniedFinal') && store.held === held) {
              dropHold();
              rest();
            }
          },
          () => {
            if (store.held !== held) return;
            dropHold();
            rest();
          },
        );
      },
      holdEnd: () => {
        const held = store.held;
        if (held === null) return;
        store.setHeld(null);
        const turn = store.turn;
        const { prior } = held;
        const failed = (failure: VoiceFailure) =>
          store.setMachine(prior ?? { kind: 'failure', failure, heard: null, source: 'voice' });
        void held.hold.release().then(
          (out) => {
            if (store.turn !== turn) return;
            if (out.kind === 'failure') {
              failed(out.failure);
              return;
            }
            // While fixing, every hold is the correction, "yes" included.
            if (prior?.fixing === true) {
              store.setMachine(prior);
              void fixHeard(internals, out.transcript);
              return;
            }
            if (prior !== null && isYes(out.transcript.text)) {
              store.setMachine(prior);
              apply();
              return;
            }
            void internals.resolve(out.transcript, { source: 'voice' });
          },
          // A recognizer that fails to start or stop never leaves 'listening'.
          () => {
            if (store.turn === turn) failed({ kind: 'nothingHeard', detail: {} });
          },
        );
      },
      cancel: () => {
        store.nextTurn();
        dropHold();
        rest();
      },
      apply,
      fix: () => startFix(internals),
      // Closes any failure sheet and asks for the Command composer: the focused screen's dock takes it, and with no
      // dock the sheet host shows it (voice/typed/composerRequest).
      typeInstead: () => {
        store.nextTurn();
        rest();
        requestTypeInstead();
      },
      submitTyped: (text) => void submitTyped(internals, text),
      retryWith: (change) => void retryWith(internals, change),
      dismiss: () => {
        store.nextTurn();
        rest();
      },
    };
  }, [state, store, e, internals, readMic, settle]);

  // The Mic tuning page sits above this screen. When it asks for "Type instead" and goes back, the screen regains
  // focus here and opens the composer (voice/micTuning/returnIntent). Outside a navigator there is no focus to follow.
  const nav = useContext(NavigationContext);
  const typeInstead = useRef(api.typeInstead);
  typeInstead.current = api.typeInstead;
  useEffect(() => {
    if (nav === undefined) return undefined;
    return nav.addListener('focus', () => {
      if (takeTypeInstead()) typeInstead.current();
    });
  }, [nav]);

  return (
    <EnginesContext.Provider value={e}>
      <VoiceInternalsContext.Provider value={internals}>
        <RegisterContext.Provider value={store.register}>
          <SpecRenderedContext.Provider value={store.specRendered}>
            <VoiceContext.Provider value={api}>{children}</VoiceContext.Provider>
          </SpecRenderedContext.Provider>
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
  const rendered = useContext(SpecRenderedContext);
  const nav = useContext(NavigationContext);
  const onView = opts?.onView;
  const entry = useRef<ScreenEntry>({ spec, onView });
  useEffect(() => {
    entry.current.spec = spec;
    entry.current.onView = onView;
    rendered?.();
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
          key={`${state.source}:${state.heard}`}
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
