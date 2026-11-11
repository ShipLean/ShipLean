// The local audio-input module: the inputs this phone can record from, and when one connects or disconnects.
// Ids are the OS's own (Android AudioDeviceInfo.id, iOS port uid): the ones the patched expo-audio stream takes as
// preferredInputId and reports as routedInputId. src/voice/native/audioInputs.ts maps them to the app's AudioInput.
import { requireOptionalNativeModule } from 'expo';

export type NativeInputKind = 'phone' | 'bluetooth' | 'wired';

export interface NativeInput {
  id: string;
  kind: NativeInputKind;
  name: string;
  connected: boolean;
}

interface AudioInputNative {
  list(): NativeInput[];
  addListener(event: 'onInputsChanged', listener: () => void): { remove(): void };
}

// The module is autolinked on a development or release build. Where it isn't (Expo Go, jest) there are no inputs.
function native(): AudioInputNative | null {
  return requireOptionalNativeModule<AudioInputNative>('ShipLeanAudioInput');
}

// The inputs the OS lists right now, in no particular order.
export function nativeInputs(): NativeInput[] {
  return native()?.list() ?? [];
}

// Calls back when an input connects or disconnects. Returns the unsubscribe.
export function onInputsChanged(cb: () => void): () => void {
  const sub = native()?.addListener('onInputsChanged', cb);
  return () => sub?.remove();
}
