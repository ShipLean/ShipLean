// Scripted voice engines for tests. Pure (no React Native), so vitest and jest specs can both use them.
import type {
  CommandModel,
  MicPermission,
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
}

// Each started session delivers the scripted partials and levels as soon as a callback is set, and stop() resolves
// the final transcript. Unscripted, stop() resolves an empty transcript (nothing heard).
export class FakeRecognizer implements SpeechRecognizer {
  readonly starts: { hotwords: string[] }[] = [];
  cancelled = 0;
  private isReady = true;
  private input: string | null = null;
  private next: RecognizerScript = { partials: [], final: aTranscript({ text: '' }), levels: [] };

  script(partials: string[], final: Transcript | string, levels: number[] = []): void {
    this.next = {
      partials,
      final: typeof final === 'string' ? aTranscript({ text: final }) : final,
      levels,
    };
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

  async start(opts: { hotwords: string[] }): Promise<RecognitionSession> {
    this.starts.push(opts);
    const { partials, final, levels } = this.next;
    return {
      onPartial: (cb) => {
        for (const p of partials) cb(p);
      },
      onLevel: (cb) => {
        for (const l of levels) cb(l);
      },
      stop: async () => final,
      cancel: () => {
        this.cancelled++;
      },
    };
  }
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
