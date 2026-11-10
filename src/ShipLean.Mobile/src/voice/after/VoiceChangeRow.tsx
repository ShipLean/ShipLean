import { View } from 'react-native';
import { voiceChangeRowId } from './VoiceChangeRow.testIds';

export interface VoiceChangeRowProps {
  itemId: string;
  itemKey: string;
}

// "Your voice change" in an item's Notes: Undo for 10 minutes after the toast.
// TODO: the item's sent voice changes from the runner, with Undo previewed, replace this empty row.
export function VoiceChangeRow({ itemId, itemKey }: VoiceChangeRowProps) {
  void itemId;
  void itemKey;
  return <View testID={voiceChangeRowId} />;
}
