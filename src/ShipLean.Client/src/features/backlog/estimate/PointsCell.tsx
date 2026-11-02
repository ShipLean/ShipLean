import type { BacklogRowDto } from '../../../api/views';
import { backlogTestIds as ids } from '../testIds';

// The points cell of a row; Tree mode renders it too.
// TODO: the click-to-edit estimate replaces this read-only cell.
export function PointsCell({ row }: { row: BacklogRowDto }) {
  return row.points === null ? (
    <span data-testid={ids.points} title="Not estimated" className="font-semibold text-caution">
      ?
    </span>
  ) : (
    <span data-testid={ids.points}>{row.points}</span>
  );
}
