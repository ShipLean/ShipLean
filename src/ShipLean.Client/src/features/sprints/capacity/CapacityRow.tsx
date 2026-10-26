import type { MemberDto } from '../../../api/projects';
import { Avatar } from '../../../components/Avatar';
import { sprintsTestIds as ids } from '../testIds';
import type { DraftRow } from './capacityDraft';

interface CapacityRowProps {
  member: MemberDto;
  row: DraftRow;
  onChange: (row: DraftRow) => void;
}

/** One member: who, their points for this sprint, and a note that explains a low number. */
export function CapacityRow({ member, row, onChange }: CapacityRowProps) {
  const { user } = member;
  return (
    <tr data-testid={ids.capacityRow}>
      <td>
        <div className="flex items-center gap-2">
          <Avatar {...user} />
          {user.name}
        </div>
      </td>
      <td className="num">
        <input
          data-testid={ids.capacityPoints}
          className="field w-20 text-right tabular-nums"
          type="number"
          min={0}
          step="any"
          inputMode="decimal"
          placeholder="—"
          aria-label={`Capacity points for ${user.name}`}
          value={row.points}
          onChange={(event) => onChange({ ...row, points: event.target.value })}
        />
      </td>
      <td>
        <input
          data-testid={ids.capacityNote}
          className="field w-full"
          placeholder="Optional, e.g. Off Thu–Fri"
          aria-label={`Note for ${user.name}`}
          value={row.note}
          onChange={(event) => onChange({ ...row, note: event.target.value })}
        />
      </td>
    </tr>
  );
}
