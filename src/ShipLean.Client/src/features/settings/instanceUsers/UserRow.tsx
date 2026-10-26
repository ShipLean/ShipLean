import { Avatar } from '../../../components/Avatar';
import { settingsTestIds } from '../testIds';
import { statusLabels } from './model';
import { UserActions, type UserAction } from './UserActions';
import type { AdminUserDto } from './usersAdminApi';

export function UserRow({
  user,
  isMe,
  onAction,
}: {
  user: AdminUserDto;
  isMe: boolean;
  onAction(action: UserAction): void;
}) {
  return (
    <tr
      data-testid={settingsTestIds.usersRow}
      data-user-id={user.id}
      className={`border-b border-edge last:border-b-0 ${user.status === 'disabled' ? 'opacity-60' : ''}`}
    >
      <td className="px-3 py-2">
        <span className="flex items-center gap-2">
          <Avatar {...user} />
          <span className="grid">
            <b data-testid={settingsTestIds.usersName} className="font-medium text-ink">
              {user.name}
              {isMe ? <span className="text-ink-muted"> (you)</span> : null}
            </b>
            <small data-testid={settingsTestIds.usersEmail} className="text-ink-muted">
              {user.email}
            </small>
          </span>
        </span>
      </td>
      <td data-testid={settingsTestIds.usersRole} className="px-3 py-2 text-ink-muted">
        {user.isInstanceAdmin ? 'Admin' : 'User'}
      </td>
      <td data-testid={settingsTestIds.usersStatus} className="px-3 py-2" data-status={user.status}>
        {statusLabels[user.status]}
      </td>
      <td data-testid={settingsTestIds.usersProjects} className="px-3 py-2 text-ink-muted">
        {user.projects.map((project) => project.key).join(' · ')}
      </td>
      <td data-testid={settingsTestIds.usersLastSignIn} className="px-3 py-2 text-ink-muted">
        {user.lastSignIn ?? 'Never'}
      </td>
      <td className="px-3 py-2 text-right">
        <UserActions user={user} isMe={isMe} onAction={onAction} />
      </td>
    </tr>
  );
}
