import type { BacklogRowDto } from '../../../api/views';
import { backlogTestIds as ids } from '../testIds';
import { LevelCell } from './LevelCell';
import type { LevelColumnId } from './levelColumns';

const numeric: readonly LevelColumnId[] = ['rank', 'points', 'features'];

// One Feature or Epic of the list. Closed items are muted.
export function LevelRow({
  row,
  columns,
  projectKey,
}: {
  row: BacklogRowDto;
  columns: readonly LevelColumnId[];
  projectKey: string;
}) {
  const closed = row.state?.category === 'completed';
  return (
    <tr
      data-testid={ids.row}
      data-key={row.key}
      className={`h-9 border-b border-edge text-[length:var(--text-sm)] ${closed ? 'text-ink-muted' : 'text-ink'}`}
    >
      {columns.map((column) => (
        <td key={column} className={`px-2 ${numeric.includes(column) ? 'text-right' : ''}`}>
          <LevelCell row={row} column={column} projectKey={projectKey} />
        </td>
      ))}
    </tr>
  );
}
