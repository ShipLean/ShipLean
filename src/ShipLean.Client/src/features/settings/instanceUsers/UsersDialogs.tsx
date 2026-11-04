import { AddUserDialog } from './AddUserDialog';
import { DisableDialog } from './DisableDialog';
import { EmailDialog } from './EmailDialog';
import type { UsersDialog } from './model';
import { PasswordDialog } from './PasswordDialog';
import type { AdminProjectChoiceDto } from './usersAdminApi';

export function UsersDialogs({
  dialog,
  projects,
  meId,
  onClose,
}: {
  dialog: UsersDialog | null;
  projects: AdminProjectChoiceDto[];
  meId: string | undefined;
  onClose(): void;
}) {
  switch (dialog?.kind) {
    case 'add':
      return <AddUserDialog projects={projects} onClose={onClose} />;
    case 'password':
      return <PasswordDialog user={dialog.user} isMe={dialog.user.id === meId} onClose={onClose} />;
    case 'disable':
      return <DisableDialog user={dialog.user} onClose={onClose} />;
    case 'email':
      return <EmailDialog user={dialog.user} isMe={dialog.user.id === meId} onClose={onClose} />;
    default:
      return null;
  }
}
