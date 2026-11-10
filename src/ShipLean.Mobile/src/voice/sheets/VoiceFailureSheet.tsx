import { useState } from 'react';
import { View } from 'react-native';
import type { ScreenVoiceSpec, VoiceFailure } from '../contracts';
import { CorrectionFailure } from '../failures/CorrectionFailure';
import { HeardFailure, type HeardFailureKind } from '../failures/HeardFailure';
import { WhatCanISay } from '../failures/WhatCanISay';
import type { RetryChange, VoiceUiStateOf } from '../internals';
import { voiceFailureSheetId } from './VoiceFailureSheet.testIds';

export interface VoiceFailureSheetProps {
  state: VoiceUiStateOf<'failure'>;
  // The focused screen's spec, for "What can I say?".
  spec: ScreenVoiceSpec | null;
  onRetry: (change: RetryChange) => void;
  onTypeInstead: () => void;
  onClose: () => void;
}

// The failures where nothing usable was heard; every other kind names what to correct.
const HEARD_KINDS: readonly HeardFailureKind[] = ['tooShort', 'nothingHeard', 'unclear'];

function isHeard(f: VoiceFailure): f is Extract<VoiceFailure, { kind: HeardFailureKind }> {
  return (HEARD_KINDS as readonly string[]).includes(f.kind);
}

// Routes a failure to its sheet: nothing usable heard, or something to correct. "What can I say?" swaps in the
// screen's command list.
export function VoiceFailureSheet({
  state,
  spec,
  onRetry,
  onTypeInstead,
  onClose,
}: VoiceFailureSheetProps) {
  const [showingCommands, setShowingCommands] = useState(false);
  const { failure, heard, source } = state;
  const whatCanISay = () => setShowingCommands(true);
  let body;
  if (showingCommands) {
    body = <WhatCanISay spec={spec} onClose={() => setShowingCommands(false)} />;
  } else if (isHeard(failure)) {
    body = (
      <HeardFailure
        failure={failure}
        heard={heard}
        source={source}
        onRetry={onRetry}
        onTypeInstead={onTypeInstead}
        onWhatCanISay={whatCanISay}
        onClose={onClose}
      />
    );
  } else {
    body = (
      <CorrectionFailure
        failure={failure}
        heard={heard}
        source={source}
        onRetry={onRetry}
        onWhatCanISay={whatCanISay}
        onClose={onClose}
      />
    );
  }
  return <View testID={voiceFailureSheetId}>{body}</View>;
}
