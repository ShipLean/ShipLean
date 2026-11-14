import { SaveBar } from '../../../../../touch/SaveBar';
import { settingsTestIds } from '../../../testIds';

function Message({ canSave, error }: { canSave: boolean; error: string | null }) {
  if (error !== null) {
    return (
      <span data-testid={settingsTestIds.profileBarError} role="alert" className="text-alert">
        {error}
      </span>
    );
  }
  return canSave ? 'Unsaved profile changes' : 'Enter a name to save';
}

/** The profile form's Save bar at 900px and below (K5, rule 15): in place of the panel's own footer, which hides there. */
export function ProfileSaveBar({
  dirty,
  canSave,
  saving,
  error,
  onSave,
  onDiscard,
}: {
  dirty: boolean;
  canSave: boolean;
  saving: boolean;
  error: string | null;
  onSave(): void;
  onDiscard(): void;
}) {
  return (
    <SaveBar
      dirty={dirty}
      message={<Message canSave={canSave} error={error} />}
      saveDisabled={!canSave}
      saving={saving}
      spacer={false}
      onSave={onSave}
      onDiscard={onDiscard}
    />
  );
}
