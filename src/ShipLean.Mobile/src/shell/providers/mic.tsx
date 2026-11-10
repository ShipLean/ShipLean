import type { AppSlotProps } from './types';

// TODO: the microphone permission and level provider replaces this pass-through.
export function MicProvider({ children }: AppSlotProps) {
  return children;
}
