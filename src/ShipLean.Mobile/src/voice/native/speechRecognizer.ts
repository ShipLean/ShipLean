// Parakeet TDT 0.6B through sherpa-onnx (react-native-sherpa-onnx), fed 16 kHz mono PCM from expo-audio's stream.
// No audio is kept: the PCM stays in memory for recognition only and is dropped when the session ends. The only files
// written are text: the screen's hotwords (under the cache directory) and the bpe vocab sherpa needs to encode them
// (next to the model files, once per load).
import { AudioModule } from 'expo-audio';
import type { AudioStreamBuffer } from 'expo-audio';
import { File, Paths } from 'expo-file-system';
import { createSTT } from 'react-native-sherpa-onnx/stt';
import type { SttEngine } from 'react-native-sherpa-onnx/stt';
import type { ModelStore, RecognitionSession, SpeechRecognizer, Transcript } from '../contracts';
import { toHotwords } from '../speech/hotwords';
import { createPartials } from '../speech/partials';
import { createPcmBuffer } from '../speech/pcmBuffer';
import { resample, TARGET_RATE } from '../speech/resample';
import { speechPresent } from '../speech/speechPresent';
import { toWords } from '../speech/words';
import type { ModelFiles } from './ports';

// Parakeet's tokens file in the manifest. The model directory is the one it lands in.
export const PARAKEET_TOKENS = 'parakeet-tokens';
// The longest hold the buffer keeps; the stream ends there.
export const MAX_HOLD_MS = 120_000;
// How often the held PCM is re-decoded for the partial transcript.
export const PARTIAL_EVERY_MS = 600;
export const BPE_VOCAB = 'bpe.vocab';
export const HOTWORDS_FILE = 'voice-hotwords.txt';

export interface SpeechRecognizerDeps {
  files: ModelFiles;
  models: ModelStore;
}

const fileUri = (path: string) => (path.startsWith('file://') ? path : `file://${path}`);
const nativePath = (uri: string) => uri.replace(/^file:\/+/, '/');

// sherpa-onnx's tokens.txt ("<piece> <id>" a line) as a sentencepiece vocab ("<piece>\t-<id>" a line), so the
// recognizer can encode hotwords with modeling unit bpe. The Parakeet package ships no bpe.vocab.
export function bpeVocabFromTokens(tokens: string): string {
  const lines: string[] = [];
  for (const line of tokens.split('\n')) {
    const at = line.lastIndexOf(' ');
    const id = line.slice(at + 1).trim();
    if (at <= 0 || !/^\d+$/.test(id)) continue;
    lines.push(`${line.slice(0, at)}\t-${id}`);
  }
  return `${lines.join('\n')}\n`;
}

// 0..1 for the level meter: the buffer's RMS on a -60..0 dBFS scale.
function level(pcm: Int16Array): number {
  if (pcm.length === 0) return 0;
  let sum = 0;
  for (const s of pcm) sum += s * s;
  const rms = Math.sqrt(sum / pcm.length) / 32768;
  if (rms <= 0) return 0;
  return Math.min(1, Math.max(0, (20 * Math.log10(rms) + 60) / 60));
}

const toFloats = (pcm: Int16Array): number[] => Array.from(pcm, (s) => s / 32768);

function samplesOf(buffer: AudioStreamBuffer): Int16Array {
  return new Int16Array(buffer.data, 0, Math.floor(buffer.data.byteLength / 2));
}

export class NativeSpeechRecognizer implements SpeechRecognizer {
  private engine: SttEngine | null = null;
  private loading: Promise<SttEngine> | null = null;

  // Without deps (the wave 0 wiring in native/index.ts) the recognizer is never ready.
  constructor(private readonly deps?: SpeechRecognizerDeps) {
    if (deps === undefined) return;
    deps.models.subscribe(() => this.follow());
    this.follow();
  }

  ready(): boolean {
    return this.engine !== null;
  }

  inputName(): string | null {
    // TODO: the microphone in use. expo-audio's stream doesn't say which input it records from; null until it does.
    return null;
  }

  async start(opts: { hotwords: string[] }): Promise<RecognitionSession> {
    const engine = await this.load();
    await this.useHotwords(engine, opts.hotwords);
    return startSession(engine);
  }

  // Loads Parakeet once the model files are checked, and frees it when the model store deletes them.
  private follow(): void {
    const phase = this.deps?.models.status().phase;
    if (phase === 'ready') {
      // A failed load resets itself, so the next start() tries again.
      this.load().catch(() => undefined);
    } else if (phase === 'none') {
      const engine = this.engine;
      const loading = this.loading;
      this.engine = null;
      this.loading = null;
      if (engine !== null) void engine.destroy();
      // A load still in flight is freed the moment it finishes.
      else if (loading !== null)
        void loading.then(
          (loaded) => loaded.destroy(),
          () => undefined,
        );
      this.removeVocab();
    }
  }

  // The folder the Parakeet files are in: the one files.path('parakeet-tokens') points into.
  private modelDir(files: ModelFiles): string {
    const tokens = new File(fileUri(files.path(PARAKEET_TOKENS)));
    return tokens.uri.slice(0, tokens.uri.lastIndexOf('/'));
  }

  // The bpe vocab is derived text; it goes with the models.
  private removeVocab(): void {
    if (this.deps === undefined) return;
    const vocab = new File(this.modelDir(this.deps.files), BPE_VOCAB);
    if (vocab.exists) vocab.delete();
  }

  private load(): Promise<SttEngine> {
    if (this.deps === undefined)
      return Promise.reject(new Error('Speech recognition is not set up'));
    if (this.loading === null) {
      const loading: Promise<SttEngine> = this.createEngine(this.deps.files).then(
        (engine) => {
          // Only a load that is still wanted becomes the engine; deleteAll may have dropped it meanwhile.
          if (this.loading === loading) this.engine = engine;
          return engine;
        },
        (error: unknown) => {
          if (this.loading === loading) this.loading = null;
          throw error;
        },
      );
      this.loading = loading;
    }
    return this.loading;
  }

  private async createEngine(files: ModelFiles): Promise<SttEngine> {
    const tokens = new File(fileUri(files.path(PARAKEET_TOKENS)));
    const modelDir = this.modelDir(files);
    const vocab = new File(modelDir, BPE_VOCAB);
    vocab.write(bpeVocabFromTokens(await tokens.text()));
    return createSTT({
      modelPath: { type: 'file', path: nativePath(modelDir) },
      modelType: 'nemo_transducer',
      preferInt8: true,
      modelingUnit: 'bpe',
      bpeVocab: nativePath(vocab.uri),
    });
  }

  // The screen's words as hotwords for this hold. Only their text is written, under the cache directory.
  private async useHotwords(engine: SttEngine, hotwords: string[]): Promise<void> {
    const text = toHotwords(hotwords);
    let hotwordsFile = '';
    if (text.trim() !== '') {
      const file = new File(Paths.cache, HOTWORDS_FILE);
      file.write(text);
      hotwordsFile = nativePath(file.uri);
    }
    try {
      await engine.setConfig({
        decodingMethod: 'modified_beam_search',
        maxActivePaths: 4,
        hotwordsFile,
      });
    } catch {
      // sherpa rejects a hotwords file it can't use. Never fail recognition over it: carry on without hotwords.
      await engine.setConfig({
        decodingMethod: 'modified_beam_search',
        maxActivePaths: 4,
        hotwordsFile: '',
      });
    }
  }
}

async function startSession(engine: SttEngine): Promise<RecognitionSession> {
  let onPartial: (text: string) => void = () => undefined;
  let onLevel: (level: number) => void = () => undefined;
  let buffer: ReturnType<typeof createPcmBuffer> | null = createPcmBuffer(MAX_HOLD_MS);
  let ended = false;

  const decode = async (pcm: Int16Array) =>
    (await engine.transcribeSamples(toFloats(pcm), TARGET_RATE)).text;
  const partials = createPartials((pcm) => decode(pcm).then((t) => t.trim()), {
    everyMs: PARTIAL_EVERY_MS,
    now: () => Date.now(),
    onText: (t) => onPartial(t),
  });

  const stream = new AudioModule.AudioStream({
    sampleRate: TARGET_RATE,
    channels: 1,
    encoding: 'int16',
  });
  const listener = stream.addListener('audioStreamBuffer', (raw: AudioStreamBuffer) => {
    if (buffer === null) return;
    const pcm = resample(samplesOf(raw), raw.sampleRate, raw.channels);
    onLevel(level(pcm));
    const room = buffer.append(pcm);
    partials.push(buffer.pcm());
    if (!room) endStream();
  });
  const endStream = () => {
    if (ended) return;
    ended = true;
    listener.remove();
    stream.stop();
  };
  try {
    await stream.start();
  } catch (error) {
    endStream();
    await partials.stop().catch(() => undefined);
    throw error;
  }

  return {
    onPartial(cb) {
      onPartial = cb;
    },
    onLevel(cb) {
      onLevel = cb;
    },
    async stop(): Promise<Transcript> {
      endStream();
      const held = buffer;
      buffer = null;
      if (held === null) throw new Error('The recognition session was cancelled');
      // A failed partial only loses the partial text; the final decode below still runs.
      await partials.stop().catch(() => undefined);
      const pcm = held.pcm();
      const durationMs = held.ms();
      if (!speechPresent(pcm)) return { text: '', words: [], durationMs };
      const result = await engine.transcribeSamples(toFloats(pcm), TARGET_RATE);
      return {
        text: result.text.trim(),
        words: toWords(result.tokens, result.timestamps),
        durationMs,
      };
    },
    cancel() {
      endStream();
      buffer = null;
      onPartial = () => undefined;
      void partials.stop().catch(() => undefined);
    },
  };
}
