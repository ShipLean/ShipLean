import { View } from 'react-native';
import type { VoiceUiStateOf } from '../internals';
import { listeningSheetId } from './ListeningSheet.testIds';

export interface ListeningSheetProps {
  state: VoiceUiStateOf<'listening'>;
}

// While the mic is held: the words so far, with hotword matches marked, and the level.
// TODO: the partial text through highlight(), the level meter and the input name replace this empty sheet.
export function ListeningSheet({ state }: ListeningSheetProps) {
  void state;
  return <View testID={listeningSheetId} />;
}
