import { RetroActionsProvider } from '../../retro/RetroActions';
import { RetroSelectionProvider } from '../../retro/lastTouched';
import type { ProjectSlotProps } from './types';

// The Retro slot: its actions (the executor) and the last touched point.
export function RetroProvider({ children }: ProjectSlotProps) {
  return (
    <RetroActionsProvider>
      <RetroSelectionProvider>{children}</RetroSelectionProvider>
    </RetroActionsProvider>
  );
}
