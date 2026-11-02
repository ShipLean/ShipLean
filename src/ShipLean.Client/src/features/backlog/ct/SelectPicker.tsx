import type { BacklogRowDto } from '../../../api/views';
import { useSelection } from '../selection/useSelection';

// CT-only: one button per row that toggles it in the selection, so a test picks rows in any order.
export function SelectPicker({ rows }: { rows: readonly BacklogRowDto[] }) {
  const selection = useSelection();
  return (
    <div>
      {rows.map((row) => (
        <button
          key={row.id}
          type="button"
          data-testid={`harness-select-${row.key}`}
          onClick={() => selection.toggle(row.id)}
        >
          {row.key}
        </button>
      ))}
    </div>
  );
}
