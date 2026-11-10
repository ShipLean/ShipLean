import type { BacklogRowDto } from '@shiplean/core/api/views';
import type { RankRow } from '../../actions/rankPlan';

// A list-mode level in rank order, as rankPlan reads it. A row with no rank (a tree group row) is not part of the order.
export function rankRows(rows: readonly BacklogRowDto[]): (RankRow & { row: BacklogRowDto })[] {
  return rows.flatMap((row) =>
    row.rank === null ? [] : [{ id: row.id, key: row.key, rank: row.rank, row }],
  );
}
