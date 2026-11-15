import { useEffect, useRef } from 'react';
import { settingsDialogTestIds as ids } from '../../features/settingsDialog/testIds';
import { SaveBar } from '../../touch/SaveBar';
import { useTouchShell } from '../../touch/useTouchShell';
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
  const { touch } = useTouchShell();
  const button =
    'h-7 rounded-control border border-edge bg-panel px-3 text-[length:var(--text-sm)] font-medium text-ink disabled:opacity-50';
  const guardSave = useRef<HTMLButtonElement>(null);
  // The guard appears in the footer while focus is on a pane or the close button: bring it to the answer.
  useEffect(() => {
    if (askingToClose) guardSave.current?.focus();
  }, [askingToClose]);
  if (touch) {
    return (
      <TouchFooter
        dirtyLabels={dirtyLabels}
        warnings={warnings}
        error={error}
        saving={saving}
        askingToClose={askingToClose}
        guardSave={guardSave}
        onRetry={onRetry}
        onCancel={onCancel}
        onSaveAndClose={onSaveAndClose}
        onSelectPane={onSelectPane}
      />
    );
  }
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

/**
 * The footer at 900px and below. The frame renders it last in the Dialog's scrolling body (not in the Dialog's footer
 * slot), so the K5 SaveBar's own spacer leaves the end of the pane clear. The SaveBar shows while a pane is unsaved:
 * Discard is Cancel, Save is the same resend as Retry (an Apply-on-save dialog resends by saving and closing).
 * The close guard, warnings and error stay as lines above it.
 */
function TouchFooter({
  dirtyLabels,
  warnings,
  error,
  saving,
  askingToClose,
  guardSave,
  onRetry,
  onCancel,
  onSaveAndClose,
  onSelectPane,
}: {
  dirtyLabels: string[];
  warnings: { id: string; message: string }[];
  error: SaveFailure | undefined;
  saving: boolean;
  askingToClose: boolean;
  guardSave: React.RefObject<HTMLButtonElement | null>;
  onRetry(): void;
  onCancel(): void;
  onSaveAndClose(): void;
  onSelectPane(id: string): void;
}) {
  const button =
    'h-[var(--touch-target)] rounded-control border border-edge bg-panel px-3 text-[length:var(--text-sm)] font-medium text-ink disabled:opacity-50';
  return (
    <>
      <div data-testid={ids.touchStatus} className="grid gap-2 empty:hidden">
        {askingToClose ? (
          <div className="flex flex-wrap items-center gap-2">
            <span
              data-testid={ids.closeGuard}
              role="alert"
              className="flex-1 text-[length:var(--text-sm)] font-semibold text-ink"
            >
              You have unsaved changes: Save or Discard.
            </span>
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
            <button
              type="button"
              data-testid={ids.closeGuardDiscard}
              className={button}
              onClick={onCancel}
            >
              Discard
            </button>
          </div>
        ) : null}
        {warnings.length > 0 ? (
          <div className="flex flex-wrap gap-2">
            {warnings.map((warning) => (
              <button
                key={warning.id}
                type="button"
                data-testid={`${ids.footerWarning}-${warning.id}`}
                className="inline-flex h-[var(--touch-target)] items-center rounded-full bg-[color-mix(in_srgb,var(--color-warn)_18%,transparent)] px-3 text-[length:var(--text-sm)] font-medium text-ink"
                onClick={() => onSelectPane(warning.id)}
              >
                {warning.message}
              </button>
            ))}
          </div>
        ) : null}
        {error === undefined ? null : (
          <div className="flex items-center gap-2">
            <span
              data-testid={ids.footerError}
              role="alert"
              className="flex-1 text-[length:var(--text-sm)] text-alert"
            >
              {error.message}
            </span>
            {error.retry ? (
              <button
                type="button"
                data-testid={ids.footerRetry}
                className={button}
                onClick={onRetry}
              >
                Retry
              </button>
            ) : null}
          </div>
        )}
      </div>
      <SaveBar
        dirty={dirtyLabels.length > 0}
        message={<span data-testid={ids.footerDirty}>Unsaved: {dirtyLabels.join(', ')}</span>}
        onDiscard={onCancel}
        onSave={onRetry}
        saving={saving}
      />
    </>
  );
}
