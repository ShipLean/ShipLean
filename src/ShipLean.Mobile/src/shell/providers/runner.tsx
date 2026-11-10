import type { ProjectSlotProps } from './types';

// TODO: a per-session InMemoryActionRunner (keyed by origin and email) wrapping ToastHost replaces this pass-through.
export function RunnerProvider({ children }: ProjectSlotProps) {
  return children;
}
