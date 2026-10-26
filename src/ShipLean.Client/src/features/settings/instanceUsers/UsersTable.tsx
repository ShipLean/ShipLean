import { settingsTestIds } from '../testIds';
import { UserRow } from './UserRow';
import type { UserAction } from './UserActions';
import type { AdminUserDto } from './usersAdminApi';

export function UsersTable({
  users,
  meId,
  onAction,
}: {
  users: AdminUserDto[];
  meId: string | undefined;
  onAction(user: AdminUserDto, action: UserAction): void;
}) {
  return (
    <table
      data-testid={settingsTestIds.usersTable}
      className="w-full text-left text-[length:var(--text-sm)]"
    >
      <thead className="border-b border-edge text-ink-muted">
        <tr>
          <th className="px-3 py-2 font-medium">Person</th>
          <th className="px-3 py-2 font-medium">Role</th>
          <th className="px-3 py-2 font-medium">Status</th>
          <th className="px-3 py-2 font-medium">Projects</th>
          <th className="px-3 py-2 font-medium">Last sign-in</th>
          <th className="px-3 py-2" />
        </tr>
      </thead>
      <tbody>
        {users.length === 0 ? (
          <tr>
            <td
              colSpan={6}
              data-testid={settingsTestIds.usersEmpty}
              className="px-3 py-4 text-center text-ink-muted"
            >
              Nobody matches.
            </td>
          </tr>
        ) : (
          users.map((user) => (
            <UserRow
              key={user.id}
              user={user}
              isMe={user.id === meId}
              onAction={(a) => onAction(user, a)}
            />
          ))
        )}
      </tbody>
    </table>
  );
}
