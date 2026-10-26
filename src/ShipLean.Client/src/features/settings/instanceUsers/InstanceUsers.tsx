import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api } from '../../../api/client';
import type { MeDto } from '../../../api/me';
import { showToast } from '../../../components/toastStore';
import { settingsTestIds } from '../testIds';
import {
  errorText,
  filterUsers,
  needsAttention,
  type UsersDialog,
  type UsersFilter,
} from './model';
import { useSignInMode } from '../../auth/useSignInMode';
import type { UserAction } from './UserActions';
import { UsersDialogs } from './UsersDialogs';
import { UsersFilters } from './UsersFilters';
import { UsersTable } from './UsersTable';
import { useInstanceUsers, useUsersAdminMutations, type AdminUserDto } from './usersAdminApi';

/** The instance admin's Users tab: everyone on the install, with actions that apply straight away. */
export function InstanceUsers() {
  const users = useInstanceUsers();
  const { proxy } = useSignInMode();
  const me = useQuery({ queryKey: ['me'], queryFn: () => api.get<MeDto>('/api/me') });
  const { unlock, setDisabled, setInstanceAdmin } = useUsersAdminMutations();
  const [search, setSearch] = useState('');
  const [filter, setFilter] = useState<UsersFilter>('all');
  const [dialog, setDialog] = useState<UsersDialog | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  if (users.isPending) {
    return (
      <p data-testid={settingsTestIds.usersLoading} className="p-4 text-ink-muted">
        Loading users…
      </p>
    );
  }
  if (users.isError) {
    return (
      <p role="alert" data-testid={settingsTestIds.usersLoadFailed} className="p-4 text-ink-muted">
        {errorText(users.error)}
      </p>
    );
  }

  const { users: all, administeredProjects } = users.data;
  const attention = all.filter(needsAttention).length;
  const activeAdmins = all.filter((u) => u.isInstanceAdmin && u.status !== 'disabled').length;

  const settle = <T,>(promise: Promise<T>, message: string) => {
    setActionError(null);
    promise.then(
      () => showToast(message, { tone: 'ok' }),
      (error: unknown) => setActionError(errorText(error)),
    );
  };

  const onAction = (user: AdminUserDto, action: UserAction) => {
    switch (action) {
      case 'password':
        return setDialog({ kind: 'password', user });
      case 'disable':
        return setDialog({ kind: 'disable', user });
      case 'email':
        return setDialog({ kind: 'email', user });
      case 'unlock':
        return settle(
          unlock.mutateAsync(user.id),
          `Unlocked ${user.name}. They can sign in again now`,
        );
      case 'enable':
        return settle(
          setDisabled.mutateAsync({ id: user.id, disabled: false }),
          proxy
            ? `Enabled ${user.name}. They can sign in again now`
            : `Enabled ${user.name}. Their old password works again`,
        );
      case 'promote':
        return settle(
          setInstanceAdmin.mutateAsync({ id: user.id, isInstanceAdmin: true }),
          `${user.name} is now an instance admin`,
        );
      case 'demote':
        return settle(
          setInstanceAdmin.mutateAsync({ id: user.id, isInstanceAdmin: false }),
          `${user.name} is no longer an instance admin`,
        );
    }
  };

  return (
    <section data-testid={settingsTestIds.usersPanel} className="grid gap-3 p-4">
      {!proxy && attention > 0 ? (
        <div
          data-testid={settingsTestIds.usersAttentionBanner}
          className="flex items-center gap-2 rounded-panel border border-edge bg-panel p-2 text-[length:var(--text-sm)] text-ink"
        >
          <b>{attention} people need you.</b>
          <span className="flex-1 text-ink-muted">
            Locked accounts, one-time passwords not used yet, and people who can’t sign in yet.
          </span>
          <button type="button" className="btn-ghost" onClick={() => setFilter('attention')}>
            Show them
          </button>
        </div>
      ) : null}
      <header className="flex flex-wrap items-center gap-2">
        <h2 className="m-0 text-[length:var(--text-md)] font-semibold text-ink">Users</h2>
        <span
          data-testid={settingsTestIds.usersCount}
          className="text-[length:var(--text-sm)] text-ink-muted"
        >
          {all.length} people · {activeAdmins} active admins
        </span>
        <span className="flex-1" />
        <input
          type="search"
          data-testid={settingsTestIds.usersSearch}
          aria-label="Search users"
          placeholder="Search name or email"
          className="field w-56"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <UsersFilters filter={filter} attention={attention} onFilter={setFilter} />
        <button
          type="button"
          data-testid={settingsTestIds.usersAdd}
          className="btn-signal"
          onClick={() => setDialog({ kind: 'add' })}
        >
          + Add user
        </button>
      </header>
      {actionError ? (
        <p
          role="alert"
          data-testid={settingsTestIds.usersActionError}
          className="m-0 text-[length:var(--text-sm)]"
          style={{ color: 'var(--color-danger)' }}
        >
          {actionError}
        </p>
      ) : null}
      <UsersTable users={filterUsers(all, search, filter)} meId={me.data?.id} onAction={onAction} />
      <p className="m-0 text-[length:var(--text-sm)] text-ink-muted">
        Changes here apply straight away. There’s no delete: a person’s name stays on history and
        comments, so disable them instead.
      </p>
      <UsersDialogs
        dialog={dialog}
        projects={administeredProjects}
        onClose={() => setDialog(null)}
      />
    </section>
  );
}
