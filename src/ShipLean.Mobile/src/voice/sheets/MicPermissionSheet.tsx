import { View } from 'react-native';
import type { MicStatus } from '../internals';
import { micPermissionSheetId } from './MicPermissionSheet.testIds';

export interface MicPermissionSheetProps {
  open: boolean;
  status: MicStatus;
  onClose: () => void;
  onTypeInstead: () => void;
}

// Why the mic is off, and the way to turn it on or type instead.
// TODO: the copy for each status, Allow or Open Settings, and Type instead replace this empty sheet.
export function MicPermissionSheet({
  open,
  status,
  onClose,
  onTypeInstead,
}: MicPermissionSheetProps) {
  void status;
  void onClose;
  void onTypeInstead;
  if (!open) return null;
  return <View testID={micPermissionSheetId} />;
}
