import type { ProjectRole } from '@shiplean/core/api/projects';
import { Avatar } from '../../../components/Avatar';
import { settingsTestIds } from '../testIds';
import type { DraftMember } from './model';

export function MemberRow({
  member,
  changed,
  readOnly,
  onRole,
  onRemove,
  onUndo,
}: {
  member: DraftMember;
  changed: boolean;
  readOnly: boolean;
  onRole(role: ProjectRole): void;
  onRemove(): void;
  onUndo(): void;
}) {
  return (
    <tr
      data-testid={settingsTestIds.memberRow}
      data-removed={member.removed}
      data-changed={changed}
      className={`border-b border-edge last:border-b-0 touch:grid touch:grid-cols-[minmax(0,1fr)_auto] touch:items-center touch:gap-x-2 touch:px-3 touch:py-2 ${changed ? 'border-l-2 border-l-warn' : ''}`}
    >
      <td
        className={`flex items-center gap-2 px-3 py-2 touch:col-span-2 touch:p-0 ${member.removed ? 'line-through opacity-60' : ''}`}
      >
        <Avatar {...member.user} />
        <span className="font-medium text-ink">{member.user.name}</span>
      </td>
      <td className="px-3 py-2 text-ink-muted touch:col-span-2 touch:p-0 touch:break-all">
        {member.email}
      </td>
      <td className="px-3 py-2 touch:col-start-1 touch:p-0 touch:pt-2">
        <select
          data-testid={settingsTestIds.memberRole}
          aria-label={`Role for ${member.user.name}`}
          className="h-7 rounded-control border border-edge bg-panel px-1 text-[length:var(--text-sm)] text-ink disabled:opacity-60 touch:min-h-[var(--touch-target)] touch:px-2"
          value={member.role}
          disabled={readOnly || member.removed}
          onChange={(e) => onRole(e.target.value === 'admin' ? 'admin' : 'member')}
        >
          <option value="admin">Admin</option>
          <option value="member">Member</option>
        </select>
      </td>
      <td className="px-3 py-2 text-right touch:col-start-2 touch:p-0 touch:pt-2">
        {readOnly ? null : member.removed ? (
          <button
            type="button"
            data-testid={settingsTestIds.memberUndo}
            className="text-[length:var(--text-sm)] text-signal touch:min-h-[var(--touch-target)] touch:px-3"
            onClick={onUndo}
          >
            Undo
          </button>
        ) : (
          <button
            type="button"
            data-testid={settingsTestIds.memberRemove}
            aria-label={`Remove ${member.user.name}`}
            className="text-ink-muted touch:min-h-[var(--touch-target)] touch:px-3"
            onClick={onRemove}
          >
            Remove
          </button>
        )}
      </td>
    </tr>
  );
}
