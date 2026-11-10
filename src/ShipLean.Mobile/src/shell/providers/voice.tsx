import { useEffect } from 'react';
import { useSession } from '../../session/useSession';
import { clearComposerRequest } from '../../voice/typed/composerRequest';
import { VoiceProvider as VoiceStateProvider, VoiceSheetsHost } from '../../voice/VoiceProvider';
import type { ProjectSlotProps } from './types';

// The voice state for the project's screens, with its sheets over them.
export function VoiceProvider({ projectKey, children }: ProjectSlotProps) {
  const { origin, email, signedIn } = useSession();
  // A composer request nobody consumed must not outlive its project or its account: it is cleared when the project
  // or the account changes, on sign-out, and when the project's screens unmount.
  useEffect(() => clearComposerRequest, [projectKey, origin, email, signedIn]);
  return (
    <VoiceStateProvider>
      {children}
      <VoiceSheetsHost />
    </VoiceStateProvider>
  );
}
