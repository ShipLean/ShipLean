import { View } from 'react-native';
import type { RetroCategory } from '@shiplean/core/api/retros';
import { retroIds } from '../testIds';

export interface AddPointSheetProps {
  projectId: string;
  sprintId: string | null;
  open: boolean;
  onClose: () => void;
  category: RetroCategory | null;
}

// TODO: the Add a point sheet replaces this stub.
export function AddPointSheet({ open }: AddPointSheetProps) {
  return open ? <View testID={retroIds.addSheet} /> : null;
}
