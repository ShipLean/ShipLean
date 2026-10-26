import type { UserStatus } from '../../../api/users';
import type { AdminUserDto } from './usersAdminApi';

export const statusLabels: Record<UserStatus, string> = {
  active: 'Active',
  oneTimePassword: 'One-time password, not used yet',
  locked: 'Locked',
  noPassword: 'No password yet',
  disabled: 'Disabled',
};

export type UsersFilter = 'all' | 'active' | 'attention' | 'disabled';

/** Locked, an unused one-time password, or nobody has set up sign-in. */
export const needsAttention = (user: AdminUserDto) =>
  user.status === 'locked' || user.status === 'oneTimePassword' || user.status === 'noPassword';

export function filterUsers(users: AdminUserDto[], search: string, filter: UsersFilter) {
  const text = search.trim().toLowerCase();
  return users.filter(
    (user) =>
      (text === '' ||
        user.name.toLowerCase().includes(text) ||
        user.email.toLowerCase().includes(text)) &&
      (filter === 'all' ||
        (filter === 'attention' && needsAttention(user)) ||
        (filter === 'disabled' && user.status === 'disabled') ||
        (filter === 'active' && user.status !== 'disabled')),
  );
}

/** Projects that would be left with no active admin if this user were disabled. */
export const projectsLosingTheirAdmin = (user: AdminUserDto) =>
  user.projects.filter((project) => project.soleActiveAdmin);

export type UsersDialog =
  | { kind: 'add' }
  | { kind: 'password'; user: AdminUserDto }
  | { kind: 'disable'; user: AdminUserDto }
  | { kind: 'email'; user: AdminUserDto };

export const errorText = (error: unknown) =>
  error instanceof Error ? error.message : 'Something went wrong. Try again.';
