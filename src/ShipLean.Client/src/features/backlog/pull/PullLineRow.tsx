import type { BacklogRowDto } from '../../../api/views';

// The pull line, drawn under the row where the forecast's cut falls.
// TODO: the line with its Pull button and drag grip replaces this empty row.
export function PullLineRow({ row, columnCount }: { row: BacklogRowDto; columnCount: number }) {
  void [row, columnCount];
  return null;
}
