import { platform } from '@shiplean/core/platform';
import { useRouter } from 'expo-router';
import { useEffect, useState } from 'react';
import { links } from '../links';
import { useVoice } from '../voice/VoiceProvider';
import { firstCommandStep, markGuide, readGuide } from './guide';

export interface FirstCommand {
  // Show the first-result sheet.
  open: boolean;
  onOpenInbox: () => void;
  onClose: () => void;
}

// The guided first command (P12): when the first voice result after the coach mark is a read-only answer, close the
// answer sheet and open the first-result sheet instead. Any other result goes on as normal and the guide stays
// pending. The guide is over once the sheet is closed either way. VW passes this to FirstResultSheet.
export function useFirstCommand(projectKey: string): FirstCommand {
  const voice = useVoice();
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const { state, cancel } = voice;

  // The first read-only answer after the coach mark opens the sheet; the answer sheet behind it is closed.
  if (!open && firstCommandStep(readGuide(platform().persistentStore()), state) === 'show') {
    setOpen(true);
  }
  useEffect(() => {
    if (open && state.kind === 'answer') cancel();
  }, [open, state.kind, cancel]);

  const finish = () => {
    markGuide(platform().persistentStore(), 'firstCommandShown');
    setOpen(false);
  };
  return {
    open,
    onClose: finish,
    onOpenInbox: () => {
      finish();
      router.push(links.inbox(projectKey, 'setup'));
    },
  };
}
