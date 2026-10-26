import { settingsTestIds } from '../../testIds';

export function SaveBar({
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
    <div className="flex items-center gap-2 rounded-b-panel border-t border-edge bg-canvas px-3 py-2 text-[length:var(--text-xs)] text-ink-muted">
      {dirty ? (
        <span data-testid={settingsTestIds.profileDirty} className="font-semibold text-caution">
          Unsaved changes
        </span>
      ) : (
        'Saved'
      )}
      {error === null ? null : (
        <span data-testid={settingsTestIds.profileSaveError} role="alert" className="text-alert">
          {error}
        </span>
      )}
      <span className="flex-1" />
      <button
        type="button"
        data-testid={settingsTestIds.profileDiscard}
        className="btn-ghost"
        disabled={!dirty || saving}
        onClick={onDiscard}
      >
        Discard
      </button>
      <button
        type="button"
        data-testid={settingsTestIds.profileSave}
        className="btn-signal disabled:opacity-50"
        disabled={!dirty || !canSave || saving}
        onClick={onSave}
      >
        Save
      </button>
    </div>
  );
}
