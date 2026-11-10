import { VoiceProvider as VoiceStateProvider, VoiceSheetsHost } from '../../voice/VoiceProvider';
import type { ProjectSlotProps } from './types';

// The voice state for the project's screens, with its sheets over them.
export function VoiceProvider({ children }: ProjectSlotProps) {
  return (
    <VoiceStateProvider>
      {children}
      <VoiceSheetsHost />
    </VoiceStateProvider>
  );
}
