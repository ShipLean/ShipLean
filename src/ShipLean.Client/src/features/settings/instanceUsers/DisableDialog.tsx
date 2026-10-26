import { Dialog } from '../../../components/Dialog';
import { showToast } from '../../../components/toastStore';
import { settingsTestIds } from '../testIds';
import { errorText, projectsLosingTheirAdmin } from './model';
import { useUsersAdminMutations, type AdminUserDto } from './usersAdminApi';

/** Disabling signs the user out everywhere; it warns for each project that would lose its last active admin. */
export function DisableDialog({ user, onClose }: { user: AdminUserDto; onClose(): void }) {
  const { setDisabled } = useUsersAdminMutations();
  const orphaned = projectsLosingTheirAdmin(user);

  const confirm = () =>
    setDisabled.mutate(
      { id: user.id, disabled: true },
      {
        onSuccess: () => {
          showToast(`Disabled ${user.name} and signed them out`, { tone: 'ok' });
          onClose();
        },
      },
    );

  return (
    <Dialog
      title={`Disable ${user.name}?`}
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
            disabled={setDisabled.isPending}
            onClick={confirm}
          >
            Disable
          </button>
        </>
      }
    >
      <p className="m-0 text-[length:var(--text-sm)] text-ink">
        They are signed out everywhere and can’t sign in. They stay a member of{' '}
        {user.projects.length} projects, and their name stays on work items, comments and history.
      </p>
      {orphaned.map((project) => (
        <p
          key={project.id}
          role="note"
          data-testid={settingsTestIds.usersDialogWarning}
          className="m-0 rounded-panel border border-edge p-2 text-[length:var(--text-sm)] text-ink"
        >
          <b>{project.key} would have no active admin.</b> Make someone else an Admin of{' '}
          {project.name} first, or join it as admin from Projects.
        </p>
      ))}
      {setDisabled.isError ? (
        <p
          role="alert"
          data-testid={settingsTestIds.usersDialogError}
          className="m-0 text-[length:var(--text-sm)]"
          style={{ color: 'var(--color-danger)' }}
        >
          {errorText(setDisabled.error)}
        </p>
      ) : null}
    </Dialog>
  );
}
