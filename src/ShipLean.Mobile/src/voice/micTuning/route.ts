import { PHONE_INPUT_ID, type AudioInput } from '../contracts';

// The picked input when it is listed and connected, otherwise the phone (voice always starts on it).
export function resolveInput(picked: string, inputs: AudioInput[]): AudioInput {
  const found = inputs.find((i) => i.id === picked && i.connected);
  if (found !== undefined) return found;
  return (
    inputs.find((i) => i.id === PHONE_INPUT_ID) ?? {
      id: PHONE_INPUT_ID,
      kind: 'phone',
      name: 'Phone',
      connected: true,
    }
  );
}
