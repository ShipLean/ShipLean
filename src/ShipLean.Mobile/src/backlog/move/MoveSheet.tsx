import type { BacklogRowDto } from '@shiplean/core/api/views';
import { View } from 'react-native';
import type { BacklogLevel } from '../actions';
import { moveSheetId } from '../testIds';

export interface MoveSheetProps {
  row: BacklogRowDto;
  // The level's rows in rank order.
  rows: BacklogRowDto[];
  level: BacklogLevel;
  open: boolean;
  onClose: () => void;
}

// TODO: the move sheet from the design (top, up, down, above and below a row).
export function MoveSheet({ open }: MoveSheetProps) {
  return open ? <View testID={moveSheetId} /> : null;
}
