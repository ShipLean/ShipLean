import { View } from 'react-native';
import type { RetroPointDto } from '@shiplean/core/api/retros';
import { retroIds } from '../testIds';

export interface DecideSheetProps {
  point: RetroPointDto;
  projectId: string;
  open: boolean;
  onClose: () => void;
}

// TODO: the Decide sheet (action item or Not needed) replaces this stub.
export function DecideSheet({ open }: DecideSheetProps) {
  return open ? <View testID={retroIds.decideSheet} /> : null;
}
