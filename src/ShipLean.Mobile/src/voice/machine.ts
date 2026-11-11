// The voice state machine's pure parts: the resting state, a hold of the mic, the prepared-prompt cache, the "yes"
// that applies an open preview, and how a resolved command becomes a preview. No React Native, so vitest runs it.
import type { AppAction, PreviewLine } from '../actions/types';
import type {
  CommandModel,
  ModelStatus,
  NoValidActionReason,
  ParsedCall,
  PreparedScreen,
  RecognitionSession,
  ResolveResult,
  ScreenVoiceSpec,
  SpeechRecognizer,
  Transcript,
  VoiceFailure,
} from './contracts';
import type { MicStatus, VoiceSource, VoiceUiState, VoiceUiStateOf } from './internals';

// A release sooner than this after the press is a tap, not a command.
export const MIN_HOLD_MS = 500;

// The resting state from the models and the mic. Precedence: skipped or no models, then downloading, then a refused
// mic once the models are ready; otherwise idle.
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

export type HoldOutcome =
  { kind: 'heard'; transcript: Transcript } | { kind: 'failure'; failure: VoiceFailure };

export interface Hold {
  // Stops listening: a release under MIN_HOLD_MS cancels the session (tooShort); no words is nothingHeard. Rejects
  // when the recognizer fails to start or stop.
  release(): Promise<HoldOutcome>;
  // Drops the session and its words.
  cancel(): void;
}

// Starts the recognizer with the screen's hotwords and passes on its partial text. now() is in milliseconds.
// Recorded PCM stays in the recognizer's memory; a hold keeps only the transcript text.
export function startHold(
  recognizer: SpeechRecognizer,
  hotwords: string[],
  now: () => number,
  onPartial: (text: string) => void,
): Hold {
  const startedAt = now();
  const session: Promise<RecognitionSession> = recognizer.start({ hotwords }).then((s) => {
    s.onPartial(onPartial);
    return s;
  });
  // A session that failed to start rejects release(); until then it is not an unhandled rejection.
  session.catch(() => {});
  return {
    release: async (): Promise<HoldOutcome> => {
      const heldMs = now() - startedAt;
      const s = await session;
      if (heldMs < MIN_HOLD_MS) {
        s.cancel();
        return { kind: 'failure', failure: { kind: 'tooShort', detail: { heldMs } } };
      }
      const transcript = await s.stop();
      if (transcript.text.trim() === '')
        return { kind: 'failure', failure: { kind: 'nothingHeard', detail: {} } };
      return { kind: 'heard', transcript };
    },
    cancel: () => {
      void session.then(
        (s) => s.cancel(),
        () => {},
      );
    },
  };
}

// What a prepared prefix depends on: the screen and its actions. Screens build their spec on every render, so the
// spec object can't be the key; prepare bakes in only the prompt prefix and grammar, and parse reads context().
function prefixKey(spec: ScreenVoiceSpec): string {
  return `${spec.screen}\u0000${spec.actions.map((a) => a.name).join(',')}`;
}

// The prepared prompt prefix for the focused screen: prepared once per screen and its actions, and kept until they
// change, so each command only parses. A rejected prepare is dropped, so the next command prepares again.
export class PreparedCache {
  private key: string | null = null;
  private prepared: Promise<PreparedScreen> | null = null;

  get(model: CommandModel, spec: ScreenVoiceSpec): Promise<PreparedScreen> {
    const key = prefixKey(spec);
    if (this.prepared === null || this.key !== key) {
      const prepared = model.prepare(spec);
      this.key = key;
      this.prepared = prepared;
      prepared.catch(() => {
        if (this.prepared === prepared) this.prepared = null;
      });
    }
    return this.prepared;
  }
}

const YES_WORDS = new Set(['yes', 'apply']);

// True when the words are only "yes" or "apply": while a preview is open that applies it, and the model isn't asked.
export function isYes(text: string): boolean {
  const words = text
    .toLowerCase()
    .split(/[^a-z]+/)
    .filter((w) => w !== '');
  return words.length > 0 && words.every((w) => YES_WORDS.has(w));
}

// Rejected parts of a command show in the preview struck out, with the reason, and are never applied.
const REJECTED_PREFIX = 'rejected:';

export function isRejectedLine(id: string): boolean {
  return id.startsWith(REJECTED_PREFIX);
}

const NO_VALID_ACTION_WHY: Record<NoValidActionReason, string> = {
  noSuchState: 'There’s no such state here.',
  notAllowed: 'That can’t be done here.',
  invalidArgs: 'Couldn’t work out what to change.',
  none: 'Couldn’t work out what to change.',
};

function whyRejected(failure: VoiceFailure): string {
  switch (failure.kind) {
    case 'webOnly':
      return `Do this on ${failure.detail.where}.`;
    case 'noMatch':
      return `Couldn’t find “${failure.detail.spoken}”.`;
    case 'misheardKey':
      return `Did you mean ${failure.detail.suggestion}?`;
    case 'ambiguous':
      return 'More than one match.';
    case 'noValidAction':
      return NO_VALID_ACTION_WHY[failure.detail.reason];
    case 'unclear':
    case 'nothingHeard':
    case 'tooShort':
      return 'Couldn’t make that out.';
  }
}

function rejectedLine(
  spec: ScreenVoiceSpec,
  r: { call: ParsedCall; failure: VoiceFailure },
  i: number,
): PreviewLine {
  const action = spec.actions.find((a) => a.name === r.call.action);
  return {
    id: `${REJECTED_PREFIX}${i}`,
    icon: 'block',
    text: action?.description ?? r.call.action,
    warning: whyRejected(r.failure),
    removable: false,
  };
}

export type ResolvedTurn =
  | { kind: 'preview'; state: VoiceUiStateOf<'preview'> }
  | { kind: 'view'; view: NonNullable<Extract<ResolveResult, { ok: true }>['view']> }
  | { kind: 'failure'; failure: VoiceFailure };

// What a resolved command shows. Changes show as a preview, with any rejected parts struck out; a command that
// changes nothing shows its view; one with nothing to do fails with the first rejected part's reason.
export function turnOf(
  spec: ScreenVoiceSpec,
  result: ResolveResult,
  heard: string,
  source: VoiceSource,
): ResolvedTurn {
  if (!result.ok) return { kind: 'failure', failure: result.failure };
  pairs(result);
  if (result.actions.length === 0) {
    if (result.view !== null) return { kind: 'view', view: result.view };
    return {
      kind: 'failure',
      failure: result.rejected[0]?.failure ?? {
        kind: 'noValidAction',
        detail: { action: null, reason: 'none' },
      },
    };
  }
  return {
    kind: 'preview',
    state: {
      kind: 'preview',
      heard,
      source,
      lines: [...result.lines, ...result.rejected.map((r, i) => rejectedLine(spec, r, i))],
      removed: [],
      marks: [],
      fixing: false,
    },
  };
}

// lines[i] describes actions[i] (the frozen ResolveResult rule): each line with its action.
export function pairs<A extends AppAction>(
  result: Extract<ResolveResult<A>, { ok: true }>,
): { line: PreviewLine; action: A }[] {
  if (result.lines.length !== result.actions.length)
    throw new Error(
      `A resolved command has ${result.lines.length} preview lines for ${result.actions.length} actions.`,
    );
  return result.actions.map((action, i) => ({ line: result.lines[i], action }));
}

// The actions whose lines are kept, in order; undefined keeps them all.
export function keptActions<A extends AppAction>(
  result: Extract<ResolveResult<A>, { ok: true }>,
  lineIds: string[] | undefined,
): A[] {
  const all = pairs(result);
  return (lineIds === undefined ? all : all.filter((p) => lineIds.includes(p.line.id))).map(
    (p) => p.action,
  );
}

// The preview's lines that Apply sends: not struck out and not removed.
export function keptLineIds(state: VoiceUiStateOf<'preview'>): string[] {
  return state.lines
    .filter((l) => !isRejectedLine(l.id) && !state.removed.includes(l.id))
    .map((l) => l.id);
}
