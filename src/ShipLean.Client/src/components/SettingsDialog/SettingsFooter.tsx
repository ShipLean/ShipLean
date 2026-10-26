import { useEffect, useRef } from 'react';
import { settingsDialogTestIds as ids } from '../../features/settingsDialog/testIds';
import type { SaveFailure } from './useSettingsDraft';

export function SettingsFooter({
  apply,
  dirtyLabels,
  warnings,
  error,
  saving,
  askingToClose,
  onSave,
  onRetry,
  onCancel,
  onSaveAndClose,
  onSelectPane,
}: {
  apply: 'onSave' | 'immediately';
  dirtyLabels: string[];
  warnings: { id: string; message: string }[];
  error: SaveFailure | undefined;
  saving: boolean;
  askingToClose: boolean;
  onSave(): void;
  onRetry(): void;
  onCancel(): void;
  onSaveAndClose(): void;
  onSelectPane(id: string): void;
}) {
  const button =
    'h-7 rounded-control border border-edge bg-panel px-3 text-[length:var(--text-sm)] font-medium text-ink disabled:opacity-50';
  const guardSave = useRef<HTMLButtonElement>(null);
  // The guard appears in the footer while focus is on a pane or the close button: bring it to the answer.
  useEffect(() => {
    if (askingToClose) guardSave.current?.focus();
  }, [askingToClose]);
  return (
    <>
      <div className="flex flex-1 flex-wrap items-center gap-x-3 gap-y-1 text-[length:var(--text-xs)]">
        {askingToClose ? (
          <span data-testid={ids.closeGuard} role="alert" className="font-semibold text-ink">
            You have unsaved changes: Save or Cancel.
          </span>
        ) : null}
        {dirtyLabels.length > 0 ? (
          <span data-testid={ids.footerDirty} className="font-semibold text-ink">
            Unsaved: {dirtyLabels.join(', ')}
          </span>
        ) : null}
        {warnings.map((warning) => (
          <button
            key={warning.id}
            type="button"
            data-testid={`${ids.footerWarning}-${warning.id}`}
            className="inline-flex h-5 items-center rounded-full bg-[color-mix(in_srgb,var(--color-warn)_18%,transparent)] px-[7px] text-[length:var(--text-xs)] font-medium text-ink"
            onClick={() => onSelectPane(warning.id)}
          >
            {warning.message}
          </button>
        ))}
        {error === undefined ? null : (
          <span data-testid={ids.footerError} role="alert" className="text-alert">
            {error.message}
          </span>
        )}
      </div>
      {error?.retry ? (
        <button type="button" data-testid={ids.footerRetry} className={button} onClick={onRetry}>
          Retry
        </button>
      ) : null}
      {askingToClose ? (
        <button
          ref={guardSave}
          type="button"
          data-testid={ids.closeGuardSave}
          className={button}
          disabled={saving}
          onClick={onSaveAndClose}
        >
          Save and close
        </button>
      ) : null}
      {apply === 'onSave' || askingToClose ? (
        <button
          type="button"
          data-testid={askingToClose ? ids.closeGuardDiscard : ids.cancel}
          className={button}
          onClick={onCancel}
        >
          Cancel
        </button>
      ) : null}
      {apply === 'onSave' ? (
        <button
          type="button"
          data-testid={ids.save}
          className="h-7 rounded-control bg-signal px-3 text-[length:var(--text-sm)] font-semibold text-signal-ink disabled:opacity-50"
          disabled={saving || dirtyLabels.length === 0}
          onClick={onSave}
        >
          Save
        </button>
      ) : null}
    </>
  );
}
