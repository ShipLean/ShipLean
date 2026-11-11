import { nativeInputs, type NativeInput } from '../../../modules/audio-input';
import { openMicStream } from './micStream';

jest.mock('../../../modules/audio-input', () => ({ nativeInputs: jest.fn() }));

// The streams the code under test opened; `routed` is what the OS says each one records from.
const created: { options: Record<string, unknown>; routed: string | null }[] = [];

jest.mock('expo-audio', () => {
  class AudioStream {
    private readonly at: number;
    constructor(options: Record<string, unknown>) {
      this.at = created.push({ options, routed: null }) - 1;
    }
    get routedInputId() {
      return created[this.at]!.routed;
    }
    addListener() {
      return { remove() {} };
    }
    async start() {}
    stop() {}
  }
  return { __esModule: true, AudioModule: { AudioStream } };
});

const builtIn: NativeInput = { id: '7', kind: 'phone', name: 'Built-in', connected: true };
const pods: NativeInput = { id: '12', kind: 'bluetooth', name: 'Pods', connected: true };

beforeEach(() => {
  created.length = 0;
  jest.mocked(nativeInputs).mockReturnValue([builtIn, pods]);
});

describe('openMicStream', () => {
  it('asks the stream for the picked input by the OS id', () => {
    openMicStream({ sampleRate: 16000, inputId: '12' });

    expect(created[0]!.options).toEqual({
      sampleRate: 16000,
      channels: 1,
      encoding: 'int16',
      preferredInputId: '12',
    });
  });

  it("asks the stream for the phone's built-in device for the phone input", () => {
    openMicStream({ sampleRate: 16000, inputId: 'phone' });

    expect(created[0]!.options.preferredInputId).toBe('7');
  });

  it('maps the device the OS routed to back to the AudioInput id', () => {
    const stream = openMicStream({ sampleRate: 16000, inputId: '12' });

    created[0]!.routed = '7';
    expect(stream.routedInputId()).toBe('phone');
    created[0]!.routed = '12';
    expect(stream.routedInputId()).toBe('12');
  });

  it('reports null when the OS says nothing, not the input that was asked for', () => {
    const stream = openMicStream({ sampleRate: 16000, inputId: '12' });

    expect(stream.routedInputId()).toBeNull();
  });
});
