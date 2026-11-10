import type { ProjectSlotProps } from './types';

// TODO: the voice engines and command provider replaces this pass-through.
export function VoiceProvider({ children }: ProjectSlotProps) {
  return children;
}
