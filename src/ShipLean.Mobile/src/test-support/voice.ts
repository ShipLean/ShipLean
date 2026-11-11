// Scripted voice engines for tests. Pure (no React Native), so vitest and jest specs can both use them.
import type {
  AudioInput,
  AudioInputs,
  CommandModel,
  MicPermission,
  MicTuningStore,
  ModelChoice,
  ModelStatus,
  ModelStore,
  ParsedCall,
  PreparedScreen,
  RecognitionSession,
  ScreenVoiceSpec,
  SpeechRecognizer,
  Transcript,
} from '../voice/contracts';
import { PHONE_INPUT_ID } from '../voice/contracts';
import { createMicTuningStore } from '../voice/micTuning/store';

// A transcript whose words are spread evenly over its duration (400 ms a word unless given).
export function aTranscript({
  text,
  durationMs,
}: {
  text: string;
  durationMs?: number;
}): Transcript {
  const tokens = text.split(/\s+/).filter((w) => w !== '');
  const total = durationMs ?? Math.max(1000, tokens.length * 400);
  const each = tokens.length === 0 ? 0 : total / tokens.length;
  return {
    text,
    words: tokens.map((w, i) => ({
      w,
      startMs: Math.round(i * each),
      endMs: Math.round((i + 1) * each),
    })),
    durationMs: total,
  };
}

interface RecognizerScript {
  partials: string[];
  final: Transcript;
  levels: number[];
  frames: number[];
}

// Each started session delivers the scripted partials and levels as soon as a callback is set, and stop() resolves
// the final transcript. Unscripted, stop() resolves an empty transcript (nothing heard).
export class FakeRecognizer implements SpeechRecognizer {
  readonly starts: { hotwords: string[] }[] = [];
  cancelled = 0;
  private isReady = true;
  private input: string | null = null;
  private inUse: AudioInput | null = null;
  private next: RecognizerScript = {
    partials: [],
    final: aTranscript({ text: '' }),
    levels: [],
    frames: [],
  };

  // frames is what the session's frames() answers: the RMS of each 30 ms frame.
  script(
    partials: string[],
    final: Transcript | string,
    levels: number[] = [],
    frames: number[] = [],
  ): void {
    this.next = {
      partials,
      final: typeof final === 'string' ? aTranscript({ text: final }) : final,
      levels,
      frames,
    };
  }

  setInputInUse(input: AudioInput | null): void {
    this.inUse = input;
  }

  setReady(ready: boolean): void {
    this.isReady = ready;
  }

  setInputName(name: string | null): void {
    this.input = name;
  }

  ready(): boolean {
    return this.isReady;
  }

  inputName(): string | null {
    return this.input;
  }

  inputInUse(): AudioInput | null {
    return this.inUse;
  }

  async start(opts: { hotwords: string[]; decodeAlways?: boolean }): Promise<RecognitionSession> {
    this.starts.push(opts);
    const { partials, final, levels, frames } = this.next;
    return {
      onPartial: (cb) => {
        for (const p of partials) cb(p);
      },
      onLevel: (cb) => {
        for (const l of levels) cb(l);
      },
      frames: () => frames,
      stop: async () => final,
      cancel: () => {
        this.cancelled++;
      },
    };
  }
}

// The phone, plus whatever a test lists. connect and disconnect change an input's state and notify.
export class FakeAudioInputs implements AudioInputs {
  private inputs: AudioInput[];
  private readonly listeners = new Set<() => void>();

  constructor(list: AudioInput[] = []) {
    this.inputs = [
      { id: PHONE_INPUT_ID, kind: 'phone', name: 'Phone', connected: true },
      ...list.filter((i) => i.id !== PHONE_INPUT_ID),
    ];
  }

  list(): AudioInput[] {
    return this.inputs;
  }

  subscribe(cb: () => void): () => void {
    this.listeners.add(cb);
    return () => {
      this.listeners.delete(cb);
    };
  }

  connect(id: string): void {
    this.inputs = this.inputs.map((i) => (i.id === id ? { ...i, connected: true } : i));
    for (const cb of [...this.listeners]) cb();
  }

  disconnect(id: string): void {
    this.inputs = this.inputs.map((i) => (i.id === id ? { ...i, connected: false } : i));
    for (const cb of [...this.listeners]) cb();
  }
}

export function fakeAudioInputs(list: AudioInput[] = []): FakeAudioInputs {
  return new FakeAudioInputs(list);
}

// A tuning store over in-memory storage (nothing reaches the device's kv store).
export function fakeTuningStore(): MicTuningStore {
  const values = new Map<string, string>();
  return createMicTuningStore({
    getItemSync: (k) => values.get(k) ?? null,
    setItemSync: (k, v) => {
      values.set(k, v);
    },
  });
}

// parse() answers the calls given to respond(), until respond() is called again.
export class FakeCommandModel implements CommandModel {
  readonly prepared: string[] = [];
  readonly parsed: { screen: string; transcript: Transcript; spec?: ScreenVoiceSpec }[] = [];
  private isReady = true;
  private calls: ParsedCall[] = [];

  respond(calls: ParsedCall[]): void {
    this.calls = calls;
  }

  setReady(ready: boolean): void {
    this.isReady = ready;
  }

  ready(): boolean {
    return this.isReady;
  }

  async prepare(spec: ScreenVoiceSpec): Promise<PreparedScreen> {
    this.prepared.push(spec.screen);
    return { screen: spec.screen };
  }

  async parse(p: PreparedScreen, t: Transcript, spec?: ScreenVoiceSpec): Promise<ParsedCall[]> {
    this.parsed.push({ screen: p.screen, transcript: t, spec });
    return this.calls;
  }
}

export type ModelStoreCall =
  | { method: 'choose'; choice: ModelChoice }
  | { method: 'download' | 'pause' | 'useMobileDataOnce' | 'skip' | 'deleteAll' }
  | { method: 'setWifiOnly'; on: boolean }
  | { method: 'reportLoadFailure'; reason: 'memory' };

// Starts ready on the 1.7B model of an 8 GB phone. set() changes the status and notifies; every call is recorded.
export class FakeModelStore implements ModelStore {
  readonly calls: ModelStoreCall[] = [];
  private current: ModelStatus = {
    phase: 'ready',
    choice: 'auto',
    resolved: 'qwen3-1.7b',
    bytesDone: 0,
    bytesTotal: 0,
    freeBytes: 32_000_000_000,
    wifiOnly: true,
    reason: null,
    files: [],
  };
  private memory = { reportedGb: 7.6, soldGb: 8 };
  private readonly listeners = new Set<() => void>();

  set(partial: Partial<ModelStatus>): void {
    this.current = { ...this.current, ...partial };
    for (const cb of [...this.listeners]) cb();
  }

  setMemory(memory: { reportedGb: number; soldGb: number }): void {
    this.memory = memory;
  }

  status(): ModelStatus {
    return this.current;
  }

  subscribe(cb: () => void): () => void {
    this.listeners.add(cb);
    return () => {
      this.listeners.delete(cb);
    };
  }

  choose(choice: ModelChoice): void {
    this.calls.push({ method: 'choose', choice });
  }

  download(): void {
    this.calls.push({ method: 'download' });
  }

  pause(): void {
    this.calls.push({ method: 'pause' });
  }

  useMobileDataOnce(): void {
    this.calls.push({ method: 'useMobileDataOnce' });
  }

  setWifiOnly(on: boolean): void {
    this.calls.push({ method: 'setWifiOnly', on });
  }

  skip(): void {
    this.calls.push({ method: 'skip' });
  }

  async deleteAll(): Promise<void> {
    this.calls.push({ method: 'deleteAll' });
  }

  deviceMemory(): { reportedGb: number; soldGb: number } {
    return this.memory;
  }

  reportLoadFailure(reason: 'memory'): void {
    this.calls.push({ method: 'reportLoadFailure', reason });
  }
}

type MicStatus = Awaited<ReturnType<MicPermission['status']>>;

// request() grants unless the status is deniedFinal or answerRequests(false) was called; it is counted either way.
export class FakeMicPermission implements MicPermission {
  requests = 0;
  settingsOpened = 0;
  private current: MicStatus = 'granted';
  private grant = true;

  set(status: MicStatus): void {
    this.current = status;
  }

  answerRequests(grant: boolean): void {
    this.grant = grant;
  }

  async status(): Promise<MicStatus> {
    return this.current;
  }

  async request(): Promise<boolean> {
    this.requests++;
    if (this.current === 'deniedFinal') return false;
    this.current = this.grant ? 'granted' : 'deniedCanAsk';
    return this.grant;
  }

  openSettings(): void {
    this.settingsOpened++;
  }
}
