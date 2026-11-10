import { View } from 'react-native';
import type { VoiceUiStateOf } from '../internals';
import { previewSheetId } from './PreviewSheet.testIds';

export interface PreviewSheetProps {
  state: VoiceUiStateOf<'preview'>;
  onApply: (lineIds: string[]) => void;
  onFix: () => void;
  onCancel: () => void;
}

// The changes a command will make, before Apply.
// TODO: the heard quote, the removable lines with their marks, Apply, Fix and Cancel replace this empty sheet.
export function PreviewSheet({ state, onApply, onFix, onCancel }: PreviewSheetProps) {
  void state;
  void onApply;
  void onFix;
  void onCancel;
  return <View testID={previewSheetId} />;
}
