import { useState } from 'react';
import { settingsTestIds } from '../testIds';
import type { Change } from './model';

export function SettingsHeader({
  projectName,
  changes,
  onJump,
  readOnly,
  saving,
  errorCount,
  onSave,
  onDiscard,
}: {
  projectName: string;
  changes: Change[];
  onJump(change: Change): void;
  readOnly: boolean;
  saving: boolean;
  /** Problems Save found and the draft still has. */
  errorCount: number;
  onSave(): void;
  onDiscard(): void;
}) {
  const changeCount = changes.length;
  const [open, setOpen] = useState(false);
  const button =
    'h-7 rounded-control border border-edge bg-panel px-3 text-[length:var(--text-sm)] font-medium text-ink disabled:opacity-50';
  return (
    <header className="flex items-center gap-3 border-b border-edge bg-panel px-4 py-3">
      <h1 className="text-[length:var(--text-lg)] font-semibold text-ink">Project settings</h1>
      <span className="text-[length:var(--text-sm)] text-ink-muted">
        {projectName} · changes apply to everyone in the project
      </span>
      <span className="flex-1" />
      {changeCount > 0 ? (
        <div className="relative">
          <button
            type="button"
            data-testid={settingsTestIds.unsaved}
            aria-expanded={open}
            className="text-[length:var(--text-sm)] font-semibold text-ink"
            onClick={() => setOpen(!open)}
          >
            {changeCount} unsaved {changeCount === 1 ? 'change' : 'changes'}
          </button>
          {open ? (
            <ul
              data-testid={settingsTestIds.changesList}
              className="absolute right-0 top-full z-10 mt-1 min-w-56 rounded-panel border border-edge bg-panel p-1"
            >
              {changes.map((change) => (
                <li key={`${change.tab}-${change.label}`}>
                  <button
                    type="button"
                    data-testid={settingsTestIds.changeItem}
                    className="w-full rounded-control px-2 py-1 text-left text-[length:var(--text-sm)] text-ink"
                    onClick={() => {
                      setOpen(false);
                      onJump(change);
                    }}
                  >
                    {change.label}
                  </button>
                </li>
              ))}
            </ul>
          ) : null}
        </div>
      ) : null}
      {readOnly ? null : (
        <>
          <button
            type="button"
            data-testid={settingsTestIds.discard}
            className={button}
            disabled={changeCount === 0 || saving}
            onClick={onDiscard}
          >
            Discard
          </button>
          <button
            type="button"
            data-testid={settingsTestIds.save}
            title="Save (Ctrl+S)"
            className="h-7 rounded-control bg-signal px-3 text-[length:var(--text-sm)] font-semibold text-signal-ink disabled:opacity-50"
            disabled={changeCount === 0 || saving}
            onClick={onSave}
          >
            {errorCount > 0 ? `Fix ${errorCount} ${errorCount === 1 ? 'error' : 'errors'}` : 'Save'}
          </button>
        </>
      )}
    </header>
  );
}
