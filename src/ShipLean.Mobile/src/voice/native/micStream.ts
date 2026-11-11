// The recognizer's only way to a microphone stream.
import { AudioModule } from 'expo-audio';
import type { AudioStreamBuffer } from 'expo-audio';
import { nativeInputs } from '../../../modules/audio-input';
import { PHONE_INPUT_ID } from '../contracts';

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

// The OS's id of the phone's own mic, or undefined when the OS lists none.
function builtInId(): string | undefined {
  return nativeInputs().find((i) => i.kind === 'phone')?.id;
}

export function openMicStream(opts: MicStreamOptions): MicStream {
  const builtIn = builtInId();
  // The phone is its own mic, never the system's choice; any other input is picked by the OS's id.
  const stream = new AudioModule.AudioStream({
    sampleRate: opts.sampleRate,
    channels: 1,
    encoding: 'int16',
    preferredInputId: opts.inputId === PHONE_INPUT_ID ? builtIn : opts.inputId,
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
    routedInputId() {
      const routed = stream.routedInputId;
      if (routed === null || routed === undefined) return null;
      return routed === builtIn ? PHONE_INPUT_ID : routed;
    },
  };
}
