import { View } from 'react-native';
import type { VoiceFailure } from '../contracts';
import type { RetryChange, VoiceSource } from '../internals';
import { heardFailureId } from './HeardFailure.testIds';

export type HeardFailureKind = 'tooShort' | 'nothingHeard' | 'unclear';

export interface HeardFailureProps {
  failure: Extract<VoiceFailure, { kind: HeardFailureKind }>;
  heard: string | null;
  source: VoiceSource;
  // { asNote: true } keeps an unclear sentence as a note.
  onRetry: (change: RetryChange) => void;
  onTypeInstead: () => void;
  onWhatCanISay: () => void;
  onClose: () => void;
}

// Too short, nothing heard, or heard but unclear: nothing was changed, and the words are kept.
// TODO: the small message for a tap or silence, and for unclear the heard quote with Save as note, Type instead and
// What can I say?, replace this empty view.
// Save as note copies the heard text word for word (copyNoteSpan); follow 'What a failure must always do'.
export function HeardFailure({
  failure,
  heard,
  source,
  onRetry,
  onTypeInstead,
  onWhatCanISay,
  onClose,
}: HeardFailureProps) {
  void failure;
  void heard;
  void source;
  void onRetry;
  void onTypeInstead;
  void onWhatCanISay;
  void onClose;
  return <View testID={heardFailureId} />;
}
