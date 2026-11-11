import { PHONE_INPUT_ID, type AudioInput, type AudioInputs } from '../contracts';

const PHONE: AudioInput = { id: PHONE_INPUT_ID, kind: 'phone', name: 'Phone', connected: true };

// TODO: list Bluetooth and wired inputs and report connects and disconnects. For now the phone only.
export class NativeAudioInputs implements AudioInputs {
  list(): AudioInput[] {
    return [PHONE];
  }

  subscribe(_cb: () => void): () => void {
    return () => {};
  }
}
