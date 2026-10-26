import { useState } from 'react';
import { showToast } from '../../../components/toastStore';
import { Dialog } from '../../../components/Dialog';
import { OneTimePasswordReveal } from '../../../components/OneTimePasswordReveal';
import { useSignInMode } from '../../auth/useSignInMode';
import { settingsTestIds } from '../testIds';
import { errorText } from './model';
import {
  useUsersAdminMutations,
  type AddUserResponse,
  type AdminProjectChoiceDto,
} from './usersAdminApi';

/** Adds a user (Member of the ticked projects, which the caller administers) and shows the one-time password once. */
export function AddUserDialog({
  projects,
  onClose,
}: {
  projects: AdminProjectChoiceDto[];
  onClose(): void;
}) {
  const { add } = useUsersAdminMutations();
  const { proxy } = useSignInMode();
  const [email, setEmail] = useState('');
  const [name, setName] = useState('');
  const [isInstanceAdmin, setIsInstanceAdmin] = useState(false);
  const [projectIds, setProjectIds] = useState<string[]>([]);
  const [added, setAdded] = useState<AddUserResponse | null>(null);

  const toggle = (id: string) =>
    setProjectIds((ids) => (ids.includes(id) ? ids.filter((x) => x !== id) : [...ids, id]));

  const submit = () =>
    add.mutate(
      { email, name, isInstanceAdmin, projectIds },
      {
        onSuccess: (response) => {
          if (response.oneTimePassword === null) {
            // No password to hand over: the identity proxy signs them in.
            showToast(`${response.user.name} is added`, { tone: 'ok' });
            onClose();
          } else {
            setAdded(response);
          }
        },
      },
    );

  if (added?.oneTimePassword) {
    return (
      <Dialog title={`${added.user.name} is added`} onClose={onClose}>
        <p className="m-0 text-[length:var(--text-sm)] text-ink">
          ShipLean doesn’t send email, so hand the password to {added.user.name} yourself. They
          choose their own the first time they sign in.
        </p>
        <OneTimePasswordReveal password={added.oneTimePassword.password} onDone={onClose} />
      </Dialog>
    );
  }

  return (
    <Dialog
      title="Add user"
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
            data-testid={settingsTestIds.usersAddSubmit}
            className="btn-signal"
            disabled={add.isPending || !email.includes('@') || name.trim() === ''}
            onClick={submit}
          >
            Add user
          </button>
        </>
      }
    >
      <label className="grid gap-1 text-[length:var(--text-sm)] text-ink">
        Email
        <input
          type="email"
          data-testid={settingsTestIds.usersAddEmail}
          className="field"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
        />
      </label>
      <label className="grid gap-1 text-[length:var(--text-sm)] text-ink">
        Name
        <input
          data-testid={settingsTestIds.usersAddName}
          className="field"
          value={name}
          onChange={(e) => setName(e.target.value)}
        />
      </label>
      <label className="flex items-center gap-2 text-[length:var(--text-sm)] text-ink">
        <input
          type="checkbox"
          data-testid={settingsTestIds.usersAddAdmin}
          checked={isInstanceAdmin}
          onChange={(e) => setIsInstanceAdmin(e.target.checked)}
        />
        Instance admin
      </label>
      <fieldset className="grid gap-1 border-0 p-0 text-[length:var(--text-sm)] text-ink">
        <legend className="mb-1">Projects</legend>
        {projects.length === 0 ? (
          <span className="text-ink-muted">You administer no projects.</span>
        ) : (
          projects.map((project) => (
            <label key={project.id} className="flex items-center gap-2">
              <input
                type="checkbox"
                data-testid={settingsTestIds.usersAddProject}
                data-project-key={project.key}
                checked={projectIds.includes(project.id)}
                onChange={() => toggle(project.id)}
              />
              <span className="font-[family-name:var(--font-mono)] text-[length:var(--text-xs)]">
                {project.key}
              </span>
              {project.name}
            </label>
          ))
        )}
        <span className="text-ink-muted">
          Added as Member. Only projects you administer are listed.
        </span>
      </fieldset>
      {proxy ? (
        <p
          data-testid={settingsTestIds.usersAddProxyNote}
          className="m-0 text-[length:var(--text-sm)] text-ink-muted"
        >
          They sign in through your identity proxy, so there is no password to hand over.
        </p>
      ) : null}
      {add.isError ? (
        <p
          role="alert"
          data-testid={settingsTestIds.usersDialogError}
          className="m-0 text-[length:var(--text-sm)]"
          style={{ color: 'var(--color-danger)' }}
        >
          {errorText(add.error)}
        </p>
      ) : null}
    </Dialog>
  );
}
