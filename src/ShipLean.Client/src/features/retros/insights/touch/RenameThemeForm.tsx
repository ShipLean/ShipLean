import { retrosTestIds as ids } from '../../testIds';

/**
 * The theme sheet's rename field (D9): a 16px name with the error under it. Discard and Save are the sheet's footer, so
 * the sheet owns the draft; Enter saves and Esc discards, as the desktop ✎ does.
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
  return (
    <label className="grid gap-1 text-[length:var(--text-sm)] font-semibold text-ink">
      Theme name
      <input
        data-testid={ids.retroThemeRenameField}
        value={value}
        autoFocus
        autoComplete="off"
        enterKeyHint="done"
        className="h-10 rounded-control border border-edge bg-canvas px-3 font-normal text-ink"
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
      {error ? (
        <span
          data-testid={ids.retroThemeRenameError}
          role="alert"
          className="text-[length:var(--text-xs)] font-normal text-alert"
        >
          {error}
        </span>
      ) : null}
    </label>
  );
}
