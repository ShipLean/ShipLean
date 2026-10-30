import { sprintsTestIds as ids } from '../testIds';
import { CapacityRow, cell, type CapacityLine } from './CapacityRow';
import type { CapacityShape, DraftRow } from './capacityDraft';

interface CapacityTableProps {
  lines: CapacityLine[];
  shape: CapacityShape;
  total: number;
  readOnly: boolean;
  /** What planning uses while the table is empty. */
  velocity: number;
  onChange: (userId: string, row: DraftRow) => void;
  onRemove: (userId: string) => void;
}

const head = `${cell} text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase`;

/** A row per member in the table, and under them the sum that planning uses, on the same column grid. */
export function CapacityTable({
  lines,
  shape,
  total,
  readOnly,
  velocity,
  onChange,
  onRemove,
}: CapacityTableProps) {
  const empty = lines.length === 0;
  return (
    <div className="relative overflow-x-auto">
      <table className="w-full min-w-[560px] table-fixed border-collapse text-[length:var(--text-sm)]">
        <colgroup>
          <col className="w-36" />
          <col className="w-24" />
          <col className="w-20" />
          <col />
          <col className="w-10" />
        </colgroup>
        <thead className="bg-sunken">
          <tr>
            <th className={`${head} text-left`}>Member</th>
            <th className={`${head} text-right`}>Days available</th>
            <th className={`${head} text-right`}>Capacity</th>
            <th className={`${head} text-left`}>Note</th>
            <th className={head}>
              <span className="sr-only">Remove</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {lines.map((line) => (
            <CapacityRow
              key={line.user.id}
              line={line}
              shape={shape}
              readOnly={readOnly}
              onChange={(row) => onChange(line.user.id, row)}
              onRemove={() => onRemove(line.user.id)}
            />
          ))}
        </tbody>
        <tfoot className="border-t border-edge bg-sunken font-semibold">
          <tr>
            <td colSpan={2} className={cell}>
              Team capacity
            </td>
            <td className={`${cell} text-right tabular-nums`}>
              <span data-testid={ids.capacityTotal}>{empty ? '—' : total}</span>
              <span className="ml-1 font-normal text-ink-muted">pts</span>
            </td>
            <td
              colSpan={2}
              data-testid={empty ? ids.capacityEmpty : undefined}
              className={`${cell} text-[length:var(--text-xs)] font-normal text-ink-muted`}
            >
              {empty
                ? `No capacity entered: planning uses velocity ${velocity}`
                : 'The sum of the column above. Planning uses only this number.'}
            </td>
          </tr>
        </tfoot>
      </table>
    </div>
  );
}
