import { waitFor } from '@testing-library/react-native';
import { File, Paths } from 'expo-file-system';
import { createSTT } from 'react-native-sherpa-onnx/stt';
import type { SttEngine, SttRecognitionResult } from 'react-native-sherpa-onnx/stt';
import { FakeModelFiles } from '../../test-support/modelFiles';
import { emitAudio, native } from '../../test-support/nativeMocks';
import { FakeModelStore } from '../../test-support/voice';
import { toHotwords } from '../speech/hotwords';
import { createPartials } from '../speech/partials';
import { createPcmBuffer } from '../speech/pcmBuffer';
import { resample } from '../speech/resample';
import { speechPresent } from '../speech/speechPresent';
import { toWords } from '../speech/words';
import { MAX_HOLD_MS, NativeSpeechRecognizer, PARTIAL_EVERY_MS } from './speechRecognizer';

// VE1a's pure modules run for real behind spies, so the spec sees how the adapter calls them. createPartials is
// replaced by a driver: the spec decides when a partial decode runs and how stop() ends.
jest.mock('../speech/hotwords', () => {
  const actual = jest.requireActual<typeof import('../speech/hotwords')>('../speech/hotwords');
  return { ...actual, toHotwords: jest.fn(actual.toHotwords) };
});
jest.mock('../speech/resample', () => {
  const actual = jest.requireActual<typeof import('../speech/resample')>('../speech/resample');
  return { ...actual, resample: jest.fn(actual.resample) };
});
jest.mock('../speech/pcmBuffer', () => {
  const actual = jest.requireActual<typeof import('../speech/pcmBuffer')>('../speech/pcmBuffer');
  return { ...actual, createPcmBuffer: jest.fn(actual.createPcmBuffer) };
});
jest.mock('../speech/speechPresent', () => {
  const actual =
    jest.requireActual<typeof import('../speech/speechPresent')>('../speech/speechPresent');
  return { ...actual, speechPresent: jest.fn(actual.speechPresent) };
});
jest.mock('../speech/words', () => {
  const actual = jest.requireActual<typeof import('../speech/words')>('../speech/words');
  return { ...actual, toWords: jest.fn(actual.toWords) };
});
jest.mock('../speech/partials', () => ({ createPartials: jest.fn() }));

type Opts = Parameters<typeof createPartials>[1];
interface PartialsDriver {
  decode: (pcm: Int16Array) => Promise<string>;
  opts: Opts;
  pushed: Int16Array[];
  stopped: number;
  stopWith: Promise<void>;
  // One partial decode of the latest PCM, its text handed to onText as createPartials does.
  tick(): Promise<void>;
}

let partials: PartialsDriver;

// URIs as expo-file-system spells them (FakeModelFiles puts the model files under /models).
const TOKENS_URI = new File('file:///models/parakeet-tokens').uri;
const VOCAB_URI = new File('file:///models/bpe.vocab').uri;
const TOKENS_TXT = '<blk> 0\n▁set 1\n▁ship 2\n906 3\n';
const HOTWORDS_URI = new File(Paths.cache, 'voice-hotwords.txt').uri;

const tone = (samples: number, amplitude = 8000) =>
  Int16Array.from({ length: samples }, (_, i) => Math.round(amplitude * Math.sin(i / 3)));

const result = (text: string, tokens: string[] = [], timestamps: number[] = []) =>
  ({
    text,
    tokens,
    timestamps,
    lang: '',
    emotion: '',
    event: '',
    durations: [],
  }) as SttRecognitionResult;

async function engineOf(index = 0): Promise<jest.Mocked<SttEngine>> {
  return (await jest.mocked(createSTT).mock.results[index].value) as jest.Mocked<SttEngine>;
}

function setUp() {
  native.files.set(TOKENS_URI, TOKENS_TXT);
  const models = new FakeModelStore();
  const recognizer = new NativeSpeechRecognizer({ files: new FakeModelFiles(), models });
  return { recognizer, models };
}

const writes = () =>
  jest.mocked(File.prototype.write).mock.contexts.map((file) => (file as File).uri);

beforeEach(() => {
  jest.mocked(createSTT).mockClear();
  jest.spyOn(File.prototype, 'write');
  jest.mocked(createPartials).mockImplementation((decode, opts) => {
    partials = {
      decode,
      opts,
      pushed: [],
      stopped: 0,
      stopWith: Promise.resolve(),
      async tick() {
        const latest = this.pushed[this.pushed.length - 1];
        opts.onText(await decode(latest));
      },
    };
    return {
      push: (pcm) => partials.pushed.push(pcm),
      stop: () => {
        partials.stopped += 1;
        return partials.stopWith;
      },
    };
  });
});

afterEach(() => {
  jest.restoreAllMocks();
});

describe('speechAdapter', () => {
  it('start passes the hotwords, partials reach onPartial, stop returns text, words and durationMs', async () => {
    const { recognizer } = setUp();
    const hotwords = ['SHIP-906', 'Active'];

    const session = await recognizer.start({ hotwords });
    const engine = await engineOf();

    expect(jest.mocked(toHotwords)).toHaveBeenCalledWith(hotwords);
    expect(native.files.get(HOTWORDS_URI)).toBe(jest.mocked(toHotwords).mock.results[0].value);
    expect(native.files.get(HOTWORDS_URI)).toContain('SHIP-906');
    expect(engine.setConfig).toHaveBeenCalledWith({
      decodingMethod: 'modified_beam_search',
      maxActivePaths: 4,
      hotwordsFile: '/cache/voice-hotwords.txt',
    });
    expect(native.streamCalls).toEqual([
      { call: 'create', sampleRate: 16000, channels: 1, encoding: 'int16' },
      { call: 'start' },
    ]);
    expect(jest.mocked(createPcmBuffer)).toHaveBeenCalledWith(MAX_HOLD_MS);
    expect(partials.opts.everyMs).toBe(PARTIAL_EVERY_MS);

    const heard: string[] = [];
    session.onPartial((t) => heard.push(t));
    for (let i = 0; i < 4; i++) emitAudio(tone(4000));

    // Every buffer goes through resample, and the partials get the whole PCM held so far.
    expect(jest.mocked(resample)).toHaveBeenCalledTimes(4);
    expect(jest.mocked(resample)).toHaveBeenLastCalledWith(expect.any(Int16Array), 16000, 1);
    expect(partials.pushed.map((p) => p.length)).toEqual([4000, 8000, 12000, 16000]);

    engine.transcribeSamples.mockResolvedValueOnce(result(' set ship'));
    await partials.tick();
    expect(heard).toEqual(['set ship']);
    const [samples, rate] = engine.transcribeSamples.mock.calls[0];
    expect(rate).toBe(16000);
    expect(samples).toHaveLength(16000);
    expect(Math.max(...samples.map(Math.abs))).toBeLessThanOrEqual(1);
    expect(Math.max(...samples.map(Math.abs))).toBeGreaterThan(0.2);

    const tokens = ['▁set', '▁ship', '906'];
    const timestamps = [0.1, 0.4, 0.7];
    engine.transcribeSamples.mockResolvedValueOnce(result(' set ship906', tokens, timestamps));
    const transcript = await session.stop();

    expect(partials.stopped).toBe(1);
    expect(engine.transcribeSamples).toHaveBeenCalledTimes(2);
    expect(engine.transcribeSamples.mock.calls[1][0]).toHaveLength(16000);
    expect(jest.mocked(toWords)).toHaveBeenCalledWith(tokens, timestamps);
    expect(transcript).toEqual({
      text: 'set ship906',
      words: jest.mocked(toWords).mock.results[0].value,
      durationMs: 1000,
    });
    expect(transcript.words.length).toBeGreaterThan(0);
    expect(native.streamCalls[native.streamCalls.length - 1]).toEqual({ call: 'stop' });
  });

  it('reports a level per buffer and the input name', async () => {
    const { recognizer } = setUp();
    const session = await recognizer.start({ hotwords: [] });
    const levels: number[] = [];
    session.onLevel((l) => levels.push(l));

    emitAudio(new Int16Array(1600).fill(3277)); // -20 dBFS
    emitAudio(new Int16Array(1600)); // silence
    emitAudio(new Int16Array(1600).fill(32767)); // full scale

    expect(levels).toHaveLength(3);
    expect(levels[0]).toBeCloseTo(2 / 3, 2);
    expect(levels[1]).toBe(0);
    expect(levels[2]).toBeCloseTo(1, 3);
    // expo-audio's stream doesn't say which input it records from.
    expect(recognizer.inputName()).toBeNull();
  });

  it('silence returns an empty transcript without a final decode', async () => {
    const { recognizer } = setUp();
    const session = await recognizer.start({ hotwords: ['SHIP-906'] });
    const engine = await engineOf();
    emitAudio(new Int16Array(8000));
    emitAudio(new Int16Array(8000));

    const transcript = await session.stop();

    expect(jest.mocked(speechPresent)).toHaveBeenCalledWith(expect.any(Int16Array));
    expect(jest.mocked(speechPresent).mock.calls[0][0]).toHaveLength(16000);
    expect(transcript).toEqual({ text: '', words: [], durationMs: 1000 });
    expect(engine.transcribeSamples).not.toHaveBeenCalled();
  });

  it('cancel discards the buffer and resolves nothing', async () => {
    const { recognizer } = setUp();
    const session = await recognizer.start({ hotwords: [] });
    const engine = await engineOf();
    const heard: string[] = [];
    session.onPartial((t) => heard.push(t));
    emitAudio(tone(8000));

    expect(session.cancel()).toBeUndefined();

    expect(native.streamCalls[native.streamCalls.length - 1]).toEqual({ call: 'stop' });
    expect(native.audioListeners).toHaveLength(0);
    expect(partials.stopped).toBe(1);
    // A partial decode that ends after the cancel reaches nobody.
    engine.transcribeSamples.mockResolvedValueOnce(result('set ship'));
    await partials.tick();
    expect(heard).toEqual([]);
    // Nothing is decoded at the end and nothing comes back.
    engine.transcribeSamples.mockClear();
    await expect(session.stop()).rejects.toThrow('cancelled');
    expect(engine.transcribeSamples).not.toHaveBeenCalled();
  });

  it('a failed partial decode never drops the final transcript', async () => {
    const { recognizer } = setUp();
    const session = await recognizer.start({ hotwords: [] });
    const engine = await engineOf();
    emitAudio(tone(16000));
    // createPartials ends the partials on a failed decode, and its stop() rejects with that error.
    partials.stopWith = Promise.reject(new Error('decode failed'));
    engine.transcribeSamples.mockResolvedValueOnce(result('set SHIP-906 to active'));

    const transcript = await session.stop();

    expect(transcript.text).toBe('set SHIP-906 to active');
    expect(transcript.durationMs).toBe(1000);
    expect(engine.transcribeSamples).toHaveBeenCalledTimes(1);
  });

  it('never writes audio to a file: the only write is the hotwords text, and no PCM or WAV is written', async () => {
    const { recognizer } = setUp();
    await waitFor(() => expect(recognizer.ready()).toBe(true));
    const before = new Map(native.files);
    jest.mocked(File.prototype.write).mockClear();

    const session = await recognizer.start({ hotwords: ['SHIP-906'] });
    emitAudio(tone(16000));
    await session.stop();

    expect(writes()).toEqual([HOTWORDS_URI]);
    const changed = [...native.files].filter(([uri, body]) => before.get(uri) !== body);
    expect(changed).toEqual([[HOTWORDS_URI, toHotwords(['SHIP-906'])]]);
    expect([...native.files.keys()].filter((uri) => /\.(wav|pcm|raw)$/i.test(uri))).toEqual([]);
  });

  it('writes the bpe vocab once from tokens.txt, next to the model files, and loads Parakeet with it', async () => {
    const { recognizer } = setUp();

    await (await recognizer.start({ hotwords: ['SHIP-906'] })).stop();
    await (await recognizer.start({ hotwords: ['Priya'] })).stop();

    expect(native.files.get(VOCAB_URI)).toBe('<blk>\t-0\n▁set\t-1\n▁ship\t-2\n906\t-3\n');
    expect(writes().filter((uri) => uri.endsWith('bpe.vocab'))).toHaveLength(1);
    expect(jest.mocked(createSTT)).toHaveBeenCalledTimes(1);
    expect(native.sttInits).toEqual([
      {
        modelPath: { type: 'file', path: '/models' },
        modelType: 'nemo_transducer',
        preferInt8: true,
        modelingUnit: 'bpe',
        bpeVocab: '/models/bpe.vocab',
      },
    ]);
    const engine = await engineOf();
    expect(engine.setConfig).toHaveBeenLastCalledWith(
      expect.objectContaining({ hotwordsFile: '/cache/voice-hotwords.txt' }),
    );
  });

  it('is never ready without deps, loads Parakeet once the models are ready and frees it on deleteAll', async () => {
    expect(new NativeSpeechRecognizer().ready()).toBe(false);
    await expect(new NativeSpeechRecognizer().start({ hotwords: [] })).rejects.toThrow();

    native.files.set(TOKENS_URI, TOKENS_TXT);
    const models = new FakeModelStore();
    models.set({ phase: 'downloading' });
    const recognizer = new NativeSpeechRecognizer({ files: new FakeModelFiles(), models });
    expect(recognizer.ready()).toBe(false);
    expect(jest.mocked(createSTT)).not.toHaveBeenCalled();

    models.set({ phase: 'ready' });
    await waitFor(() => expect(recognizer.ready()).toBe(true));
    models.set({ phase: 'ready', bytesDone: 1 });
    expect(jest.mocked(createSTT)).toHaveBeenCalledTimes(1);
    const engine = await engineOf();
    expect(engine.destroy).not.toHaveBeenCalled();

    models.set({ phase: 'none' });
    expect(recognizer.ready()).toBe(false);
    expect(engine.destroy).toHaveBeenCalledTimes(1);
  });
});
