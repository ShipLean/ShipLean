import type { BacklogRowDto } from '@shiplean/core/api/views';
import { View } from 'react-native';
import type { BacklogLevel } from '../actions';
import { backlogRowId } from '../testIds';

export interface BacklogRowProps {
  row: BacklogRowDto;
  level: BacklogLevel;
  // Marks the rows a move shifted: those from this rank on.
  highlight: { from: number } | null;
  onMove: (row: BacklogRowDto) => void;
  onOpen: (row: BacklogRowDto) => void;
}

// TODO: the row from the design (type, key, title, rank, health, needs-you and blocked marks).
export function BacklogRow({ row }: BacklogRowProps) {
  return <View testID={backlogRowId(row.key)} />;
}
