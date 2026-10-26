import { useState } from 'react';
import { Dialog } from '../../../components/Dialog';
import { OneTimePasswordReveal } from '../../../components/OneTimePasswordReveal';
import { settingsTestIds } from '../testIds';
import { errorText } from './model';
import { useUsersAdminMutations, type AdminUserDto } from './usersAdminApi';

/** Reset password, or Set up sign-in for a user with no password: confirm, then show the one-time password once. */
export function PasswordDialog({ user, onClose }: { user: AdminUserDto; onClose(): void }) {
  const { issuePassword } = useUsersAdminMutations();
  const [password, setPassword] = useState<string | null>(null);
  const setup = user.status === 'noPassword';

  const confirm = () =>
    issuePassword.mutate(user.id, { onSuccess: (issued) => setPassword(issued.password) });

  return (
    <Dialog
      title={setup ? `Set up sign-in for ${user.name}` : `Reset ${user.name}’s password?`}
      onClose={onClose}
      footer={
        password === null ? (
          <>
            <span className="flex-1" />
            <button
              type="button"
              data-testid={settingsTestIds.usersDialogCancel}
              className="btn-ghost"
              onClick={onClose}
            >
              Cancel
            </button>
            <button
              type="button"
              data-testid={settingsTestIds.usersDialogConfirm}
              className="btn-signal"
              disabled={issuePassword.isPending}
              onClick={confirm}
            >
              {setup ? 'Create one-time password' : 'Reset password'}
            </button>
          </>
        ) : null
      }
    >
      {password === null ? (
        <p className="m-0 text-[length:var(--text-sm)] text-ink">
          {setup
            ? 'They have no password yet. This gives you a one-time password to hand over.'
            : 'This signs them out everywhere and gives you a one-time password to hand over. Their current password stops working straight away.'}
        </p>
      ) : (
        <OneTimePasswordReveal password={password} onDone={onClose} />
      )}
      {issuePassword.isError ? (
        <p
          role="alert"
          data-testid={settingsTestIds.usersDialogError}
          className="m-0 text-[length:var(--text-sm)]"
          style={{ color: 'var(--color-danger)' }}
        >
          {errorText(issuePassword.error)}
        </p>
      ) : null}
    </Dialog>
  );
}
