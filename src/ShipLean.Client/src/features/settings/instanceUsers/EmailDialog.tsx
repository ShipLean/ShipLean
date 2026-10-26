import { useState } from 'react';
import { Dialog } from '../../../components/Dialog';
import { showToast } from '../../../components/toastStore';
import { settingsTestIds } from '../testIds';
import { errorText } from './model';
import { useUsersAdminMutations, type AdminUserDto } from './usersAdminApi';

export function EmailDialog({ user, onClose }: { user: AdminUserDto; onClose(): void }) {
  const { changeEmail } = useUsersAdminMutations();
  const [email, setEmail] = useState(user.email);

  const save = () =>
    changeEmail.mutate(
      { id: user.id, email },
      {
        onSuccess: () => {
          showToast(`${user.name} now signs in as ${email.trim()} and is signed out everywhere`, {
            tone: 'ok',
          });
          onClose();
        },
      },
    );

  return (
    <Dialog
      title={`Change ${user.name}’s email`}
      onClose={onClose}
      footer={
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
            disabled={changeEmail.isPending || email.trim() === ''}
            onClick={save}
          >
            Save email
          </button>
        </>
      }
    >
      <label className="grid gap-1 text-[length:var(--text-sm)] text-ink">
        Email
        <input
          type="email"
          data-testid={settingsTestIds.usersEmailInput}
          className="field"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
        />
      </label>
      <p className="m-0 text-[length:var(--text-sm)] text-ink-muted">
        It is what they sign in with. They are signed out everywhere and use the new address next
        time.
      </p>
      {changeEmail.isError ? (
        <p
          role="alert"
          data-testid={settingsTestIds.usersDialogError}
          className="m-0 text-[length:var(--text-sm)]"
          style={{ color: 'var(--color-danger)' }}
        >
          {errorText(changeEmail.error)}
        </p>
      ) : null}
    </Dialog>
  );
}
