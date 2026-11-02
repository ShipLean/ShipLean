import type { BacklogRowDto } from '../../../api/views';
import { backlogTestIds as ids } from '../testIds';

// The checkbox cell at the start of a Stories list or planning row.
// TODO: the hover checkbox, checked state and range select replace this empty cell.
export function RowSelectCell({ row }: { row: BacklogRowDto }) {
  return <td data-testid={ids.rowSelect} data-row-id={row.id} className="w-8 px-1 text-center" />;
}
