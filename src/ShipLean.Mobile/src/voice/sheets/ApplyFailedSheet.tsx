import { View } from 'react-native';
import type { VoiceUiStateOf } from '../internals';
import { applyFailedSheetId } from './ApplyFailedSheet.testIds';

export interface ApplyFailedSheetProps {
  state: VoiceUiStateOf<'applyFailed'>;
  onRetry: () => void;
  onQueue: () => void;
  onDiscard: () => void;
}

// An applied voice batch that failed: what stayed applied, what was reversed and what wasn't sent.
// TODO: the lines with their outcomes, Try again, Send when online and Discard replace this empty sheet.
export function ApplyFailedSheet({ state, onRetry, onQueue, onDiscard }: ApplyFailedSheetProps) {
  void state;
  void onRetry;
  void onQueue;
  void onDiscard;
  return <View testID={applyFailedSheetId} />;
}
