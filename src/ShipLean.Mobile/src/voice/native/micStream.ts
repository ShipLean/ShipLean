// The recognizer's only way to a microphone stream.
import { AudioModule } from 'expo-audio';
import type { AudioStreamBuffer } from 'expo-audio';

export interface MicStreamOptions {
  sampleRate: number;
  inputId: string;
}

export interface MicStream {
  onBuffer(cb: (pcm: Int16Array, sampleRate: number, channels: number) => void): {
    remove(): void;
  };
  start(): Promise<void>;
  stop(): void;
  // After start(): the AudioInput id the OS routed to, or null when it doesn't say.
  routedInputId(): string | null;
}

export function openMicStream(opts: MicStreamOptions): MicStream {
  // TODO: choose the device with opts.inputId and report the routed one; expo-audio's stream has no device option.
  const stream = new AudioModule.AudioStream({
    sampleRate: opts.sampleRate,
    channels: 1,
    encoding: 'int16',
  });
  return {
    onBuffer(cb) {
      return stream.addListener('audioStreamBuffer', (raw: AudioStreamBuffer) =>
        cb(
          new Int16Array(raw.data, 0, Math.floor(raw.data.byteLength / 2)),
          raw.sampleRate,
          raw.channels,
        ),
      );
    },
    start: () => stream.start(),
    stop: () => stream.stop(),
    routedInputId: () => null,
  };
}
