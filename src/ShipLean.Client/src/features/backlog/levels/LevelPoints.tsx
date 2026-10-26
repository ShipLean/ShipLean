import type { BacklogRowDto } from '../../../api/views';
import { backlogTestIds as ids } from '../testIds';

// The item's own estimate with the sum of everything under it greyed beside it (`20 · Σ26`), in warn when the server says
// the sum is more than a quarter over.
export function LevelPoints({ row }: { row: BacklogRowDto }) {
  return (
    <span data-testid={ids.levelPoints}>
      {row.points === null ? (
        <span title="Not estimated" className="font-semibold text-caution">
          ?
        </span>
      ) : (
        row.points
      )}
      {row.childPoints === null ? null : (
        <span
          data-testid={ids.levelPointsSum}
          data-warn={row.sumWarn}
          title="Sum of everything under it"
          className={row.sumWarn ? 'font-semibold text-caution' : 'text-ink-muted'}
        >
          {' · '}Σ{row.childPoints}
        </span>
      )}
    </span>
  );
}
