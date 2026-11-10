// N9: the voice, model and setup seams. Speak in, read out: voice is input only, and no audio is kept. Recorded PCM
// stays in memory for recognition only; only the words the person approves are posted.
import type { AppAction, PreviewLine } from '../actions/types';
import type { Link } from '../links';

export type JsonSchema = Record<string, unknown>;

export interface Transcript {
  text: string;
  words: { w: string; startMs: number; endMs: number }[];
  durationMs: number;
}

export interface RecognitionSession {
  onPartial(cb: (text: string) => void): void;
  // 0..1, for the listening sheet's level meter.
  onLevel(cb: (level: number) => void): void;
  stop(): Promise<Transcript>;
  cancel(): void;
}

export interface SpeechRecognizer {
  ready(): boolean;
  // The microphone in use ("AirPods Pro"), or null when the phone doesn't say.
  inputName(): string | null;
  start(opts: { hotwords: string[] }): Promise<RecognitionSession>;
}

export interface VoiceActionSpec {
  name: string;
  description: string;
  args: JsonSchema;
  // The argument the app copies word for word from the transcript; the model returns its noteSpan only.
  noteArg?: string;
  // Where to do it instead, for an action the phone can't take.
  webOnly?: string;
}

export interface ParsedCall {
  action: string;
  args: Record<string, unknown>;
  noteSpan?: { start: number; end: number };
}

// Every spec's answer view uses it.
export interface AnswerSheetContent {
  lead: string;
  rows: { key: string; title: string; counts: string }[];
}

export type ViewIntent =
  | { kind: 'navigate'; link: Link<string> }
  | { kind: 'answer'; content: AnswerSheetContent }
  | { kind: 'screen'; command: string; args: Record<string, unknown> };

export type NoValidActionReason = 'noSuchState' | 'notAllowed' | 'invalidArgs' | 'none';

// The kind decides the detail. Which failure a call gets is decided only in resolve.ts.
export type VoiceFailure =
  | { kind: 'nothingHeard'; detail: Record<string, never> }
  | { kind: 'tooShort'; detail: { heldMs: number } }
  | { kind: 'unclear'; detail: { heard: string } }
  | {
      kind: 'noMatch';
      detail: { spoken: string; role: string; span: { start: number; end: number } | null };
    }
  | { kind: 'misheardKey'; detail: { spoken: string; suggestion: string } }
  | { kind: 'noValidAction'; detail: { action: string | null; reason: NoValidActionReason } }
  | { kind: 'webOnly'; detail: { action: string; where: string } }
  | {
      kind: 'ambiguous';
      detail: {
        candidates: { key: string; title: string; rank?: number }[];
        call: ParsedCall;
      };
    };

export type VoiceFailureKind = VoiceFailure['kind'];

export type ResolveResult<A = AppAction> =
  | {
      ok: true;
      actions: A[];
      lines: PreviewLine[];
      rejected: { call: ParsedCall; failure: VoiceFailure }[];
      view: ViewIntent | null;
    }
  | { ok: false; failure: VoiceFailure };

export interface ScreenVoiceSpec<A extends AppAction = AppAction> {
  screen: string;
  actions: VoiceActionSpec[];
  context(): Record<string, unknown>;
  hotwords(): string[];
  toActions(calls: ParsedCall[], transcript: Transcript): ResolveResult<A>;
}

// What a matcher found. T0r's matchers are exact (match or none); fuzzy matching adds misheard.
export type Match<T> =
  | { kind: 'match'; value: T }
  | { kind: 'misheard'; heard: string; suggestion: T }
  | { kind: 'none' };

export interface PreparedScreen {
  readonly screen: string;
}

export interface CommandModel {
  ready(): boolean;
  prepare(spec: ScreenVoiceSpec): Promise<PreparedScreen>;
  // spec is the screen's current spec: its context() and hotwords are read here, never frozen at prepare (amendment 25).
  parse(p: PreparedScreen, t: Transcript, spec?: ScreenVoiceSpec): Promise<ParsedCall[]>;
}

export type ModelChoice = 'auto' | 'qwen3-1.7b' | 'qwen3-4b';

export type CommandModelId = 'qwen3-1.7b' | 'qwen3-4b';

export interface ModelStatus {
  phase: 'none' | 'skipped' | 'downloading' | 'paused' | 'checking' | 'ready' | 'failed';
  choice: ModelChoice;
  resolved: CommandModelId | null;
  bytesDone: number;
  bytesTotal: number;
  freeBytes: number;
  wifiOnly: boolean;
  reason: 'wifi' | 'space' | 'checksum' | 'network' | 'memory' | null;
  files: { id: string; state: 'pending' | 'downloading' | 'checked' | 'failed'; bytes: number }[];
}

export interface ModelStore {
  status(): ModelStatus;
  subscribe(cb: () => void): () => void;
  choose(c: ModelChoice): void;
  download(): void;
  pause(): void;
  useMobileDataOnce(): void;
  // The Wi-Fi only switch (setup's model screen and Settings › Voice); status().wifiOnly reads it.
  setWifiOnly(on: boolean): void;
  skip(): void;
  deleteAll(): Promise<void>;
  deviceMemory(): { reportedGb: number; soldGb: number };
  // The model didn't fit in memory when loaded.
  reportLoadFailure(reason: 'memory'): void;
}

export interface MicPermission {
  status(): Promise<'granted' | 'undetermined' | 'deniedCanAsk' | 'deniedFinal'>;
  request(): Promise<boolean>;
  openSettings(): void;
}

export interface VoiceEngines {
  recognizer: SpeechRecognizer;
  model: CommandModel;
  models: ModelStore;
  mic: MicPermission;
}

// One row of a screen's command test set (tools/voice-eval).
export interface TestSetRow {
  screen: string;
  utterance: string;
  calls: ParsedCall[];
  note: string | null;
  context?: Record<string, unknown>;
}
