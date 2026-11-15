import { useState } from 'react';
import type { RetroThemeRefDto } from '@shiplean/core/api/retros';
import { retrosTestIds as ids } from '../../testIds';
import { useRenameTheme } from './useRenameTheme';

/** A theme's name with an edit button; editing renames it (`PUT api/retros/themes/{id}`). */
export function ThemeNameCell({
  projectId,
  theme,
}: {
  projectId: string;
  theme: RetroThemeRefDto;
}) {
  const [draft, setDraft] = useState<string | null>(null);
  const { rename, pending, error, clearError } = useRenameTheme(projectId, theme.id, () =>
    setDraft(null),
  );
  const cancel = () => {
    setDraft(null);
    clearError();
  };
  const save = () => {
    const name = (draft ?? '').trim();
    if (name === '' || name === theme.name) {
      cancel();
      return;
    }
    rename(name);
  };

  if (draft === null) {
    return (
      <div data-testid={ids.retroThemeRename} className="flex items-center gap-1.5">
        <b className="text-[length:var(--text-sm)]">{theme.name}</b>
        <button
          type="button"
          data-action="edit"
          aria-label={`Rename ${theme.name}`}
          className="cursor-pointer text-ink-muted hover:text-ink"
          onClick={() => setDraft(theme.name)}
        >
          ✎
        </button>
      </div>
    );
  }
  return (
    <div data-testid={ids.retroThemeRename} className="grid gap-1">
      <div className="flex items-center gap-1">
        <input
          data-field="name"
          aria-label="Theme name"
          value={draft}
          autoFocus
          className="min-w-0 flex-1 rounded-control border border-edge bg-panel px-2 py-0.5 text-[length:var(--text-sm)]"
          onChange={(event) => setDraft(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === 'Enter') save();
            if (event.key === 'Escape') {
              event.preventDefault();
              cancel();
            }
          }}
        />
        <button
          type="button"
          data-action="save"
          className="cursor-pointer rounded-control border border-edge bg-panel px-2 py-0.5 text-[length:var(--text-xs)]"
          disabled={pending}
          onClick={save}
        >
          Save
        </button>
        <button
          type="button"
          data-action="cancel"
          className="cursor-pointer text-[length:var(--text-xs)] text-ink-muted"
          onClick={cancel}
        >
          Cancel
        </button>
      </div>
      {error ? (
        <span data-error="" role="alert" className="text-[length:var(--text-xs)] text-alert">
          {error}
        </span>
      ) : null}
    </div>
  );
}
