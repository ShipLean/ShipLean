import type { MemberDto } from '../../../api/projects';
import { sprintsTestIds as ids } from '../testIds';
import { CapacityRow } from './CapacityRow';
import { rowOf, type CapacityDraft, type DraftRow } from './capacityDraft';

interface CapacityTableProps {
  members: MemberDto[];
  draft: CapacityDraft;
  total: number;
  entered: boolean;
  velocity: number | undefined;
  onChange: (userId: string, row: DraftRow) => void;
}

/** A row per project member, and under them the sum that planning uses. */
export function CapacityTable({
  members,
  draft,
  total,
  entered,
  velocity,
  onChange,
}: CapacityTableProps) {
  return (
    <div className="overflow-x-auto">
      <table className="list">
        <thead>
          <tr>
            <th>Member</th>
            <th className="num w-[130px]">Capacity (pts)</th>
            <th>Note</th>
          </tr>
        </thead>
        <tbody>
          {members.map((member) => (
            <CapacityRow
              key={member.user.id}
              member={member}
              row={rowOf(draft, member.user.id)}
              onChange={(row) => onChange(member.user.id, row)}
            />
          ))}
        </tbody>
        <tfoot>
          <tr>
            <td>Team capacity</td>
            <td className="num" data-testid={ids.capacityTotal}>
              {entered ? total : velocity === undefined ? '—' : `— (velocity ${velocity})`}
            </td>
            <td className="text-[length:var(--text-xs)] font-normal text-ink-muted">
              The sum of the column above. Planning uses only this number.
            </td>
          </tr>
        </tfoot>
      </table>
    </div>
  );
}
