import { View } from 'react-native';
import type { VoiceFailure } from '../contracts';
import type { RetryChange, VoiceSource } from '../internals';
import { correctionFailureId } from './CorrectionFailure.testIds';

export type CorrectionFailureKind = Exclude<
  VoiceFailure['kind'],
  'tooShort' | 'nothingHeard' | 'unclear'
>;

export interface CorrectionFailureProps {
  failure: Extract<VoiceFailure, { kind: CorrectionFailureKind }>;
  heard: string | null;
  source: VoiceSource;
  // The one-tap correction: a different value for the span that didn't match.
  onRetry: (change: RetryChange) => void;
  onWhatCanISay: () => void;
  onClose: () => void;
}

// No match, a misheard key, no valid action, web only or ambiguous: nothing was changed; offer the correction.
// TODO: the heard quote with the miss underlined and each kind's suggestions or "where to do it" replace this.
export function CorrectionFailure({
  failure,
  heard,
  source,
  onRetry,
  onWhatCanISay,
  onClose,
}: CorrectionFailureProps) {
  void failure;
  void heard;
  void source;
  void onRetry;
  void onWhatCanISay;
  void onClose;
  return <View testID={correctionFailureId} />;
}
