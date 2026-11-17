import { useId } from 'react';
import { retrosTestIds as ids } from '../../testIds';

/**
 * The theme sheet's rename field (D9): a 16px name, the error under it, and the line saying what a rename keeps. Discard
 * and Save are the sheet's footer, so the sheet owns the draft; Enter saves and Esc discards, as the desktop ✎ does.
 */
export function RenameThemeForm({
  value,
  error,
  onChange,
  onSave,
  onDiscard,
}: {
  value: string;
  error: string | null;
  onChange: (value: string) => void;
  onSave: () => void;
  onDiscard: () => void;
}) {
  const helpId = useId();
  return (
    <div className="grid gap-1 pb-1">
      <label className="grid gap-1 text-[length:var(--text-sm)] font-semibold text-ink">
        Theme name
        <input
          data-testid={ids.retroThemeRenameField}
          value={value}
          autoFocus
          autoComplete="off"
          enterKeyHint="done"
          aria-describedby={helpId}
          className="h-10 rounded-control border border-edge bg-canvas px-3 font-normal text-ink focus:ring-1 focus:ring-signal focus:outline-none"
          onChange={(event) => onChange(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === 'Enter') onSave();
            if (event.key === 'Escape') {
              // The sheet closes on an Esc nobody has handled; this one only leaves the rename.
              event.preventDefault();
              onDiscard();
            }
          }}
        />
      </label>
      {error ? (
        <span
          data-testid={ids.retroThemeRenameError}
          role="alert"
          className="text-[length:var(--text-xs)] text-alert"
        >
          {error}
        </span>
      ) : null}
      <p
        id={helpId}
        data-testid={ids.retroThemeRenameHelper}
        className="m-0 text-[length:var(--text-sm)] text-ink-muted"
      >
        Renaming keeps every point, action item and count. Agents see the new name in their next
        brief.
      </p>
    </div>
  );
}
