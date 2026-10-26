import { useState } from 'react';
import { useSignInMode } from '../../auth/useSignInMode';
import { settingsTestIds } from '../testIds';
import type { AdminUserDto } from './usersAdminApi';

export type UserAction =
  'password' | 'unlock' | 'promote' | 'demote' | 'disable' | 'enable' | 'email';

interface Item {
  action: UserAction;
  label: string;
  testId: string;
  reason?: string;
}

function itemsFor(user: AdminUserDto, isMe: boolean, proxy: boolean): Item[] {
  if (user.status === 'disabled') {
    return [
      { action: 'enable', label: 'Enable', testId: settingsTestIds.usersMenuDisable },
      ...(proxy
        ? []
        : [{ action: 'email', label: 'Change email…', testId: settingsTestIds.usersMenuEmail }]),
    ] as Item[];
  }
  // The identity proxy owns passwords and email, so those items are not offered in proxy mode.
  const items: Item[] = proxy
    ? []
    : [
        {
          action: 'password',
          label: user.status === 'noPassword' ? 'Set up sign-in…' : 'Reset password…',
          testId: settingsTestIds.usersMenuPassword,
          reason: isMe ? 'Change your own on Your profile' : undefined,
        },
      ];
  if (user.status === 'locked') {
    items.push({ action: 'unlock', label: 'Unlock', testId: settingsTestIds.usersMenuUnlock });
  }
  items.push(
    {
      action: user.isInstanceAdmin ? 'demote' : 'promote',
      label: user.isInstanceAdmin ? 'Remove admin' : 'Make admin',
      testId: settingsTestIds.usersMenuAdmin,
      reason: isMe && user.isInstanceAdmin ? "You can't remove your own admin rights" : undefined,
    },
    ...(proxy
      ? []
      : [
          {
            action: 'email',
            label: 'Change email…',
            testId: settingsTestIds.usersMenuEmail,
            reason: isMe ? 'Change your own on Your profile' : undefined,
          } as Item,
        ]),
    {
      action: 'disable',
      label: 'Disable…',
      testId: settingsTestIds.usersMenuDisable,
      reason: isMe ? "You can't disable yourself" : undefined,
    },
  );
  return items;
}

/** The row's action menu. An action that is refused for this person is disabled and says why. */
export function UserActions({
  user,
  isMe,
  onAction,
}: {
  user: AdminUserDto;
  isMe: boolean;
  onAction(action: UserAction): void;
}) {
  const [open, setOpen] = useState(false);
  const { proxy } = useSignInMode();
  return (
    <div className="relative inline-block">
      <button
        type="button"
        data-testid={settingsTestIds.usersActions}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label={`Actions for ${user.name}`}
        className="btn-ghost"
        onClick={() => setOpen(!open)}
      >
        ⋯
      </button>
      {open ? (
        <div
          role="menu"
          data-testid={settingsTestIds.usersMenu}
          className="absolute right-0 z-20 mt-1 grid min-w-56 rounded-panel border border-edge bg-panel p-1 shadow-panel"
        >
          {itemsFor(user, isMe, proxy).map((item) => (
            <button
              key={item.action}
              type="button"
              role="menuitem"
              data-testid={item.testId}
              disabled={item.reason !== undefined}
              title={item.reason}
              className="grid rounded-control px-2 py-1 text-left text-[length:var(--text-sm)] text-ink hover:bg-sunken disabled:opacity-50"
              onClick={() => {
                setOpen(false);
                onAction(item.action);
              }}
            >
              {item.label}
              {item.reason ? <small className="text-ink-muted">{item.reason}</small> : null}
            </button>
          ))}
        </div>
      ) : null}
    </div>
  );
}
