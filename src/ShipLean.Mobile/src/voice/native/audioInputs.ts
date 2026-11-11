import { nativeInputs, onInputsChanged, type NativeInput } from '../../../modules/audio-input';
import {
  PHONE_INPUT_ID,
  type AudioInput,
  type AudioInputKind,
  type AudioInputs,
} from '../contracts';

const PHONE: AudioInput = { id: PHONE_INPUT_ID, kind: 'phone', name: 'Phone', connected: true };

const ORDER: Record<AudioInputKind, number> = { phone: 0, bluetooth: 1, wired: 2 };

function toInput(input: NativeInput): AudioInput {
  return { id: input.id, kind: input.kind, name: input.name, connected: input.connected };
}

// The inputs the OS lists: the phone first (always there, always connected), then Bluetooth, then wired.
export class NativeAudioInputs implements AudioInputs {
  list(): AudioInput[] {
    const others = nativeInputs()
      .filter((i) => i.kind !== 'phone')
      .map(toInput);
    return [...others, PHONE].sort((a, b) => ORDER[a.kind] - ORDER[b.kind]);
  }

  subscribe(cb: () => void): () => void {
    return onInputsChanged(cb);
  }
}
