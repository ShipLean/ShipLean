import { useState } from 'react';
import { RETRO_CATEGORIES, type RetroCategory } from '../../../../api/retros';
import { Dialog } from '../../../../components/Dialog';
import { retroCategoryLabels } from '../../../retroShared/retroCategories';
import { ThemePicker } from '../../../retroShared/ThemePicker';
import { useRetroThemes } from '../../useRetroQueries';
import { retrosTestIds as ids } from '../../testIds';
import type { SprintRetroSlotProps } from '../SprintRetroSlotProps';
import { subjectItems } from './subjects';
import { useCreatePoint } from './useCreatePoint';

type ThemeChoice = { id: string; name: string } | { id: null; name: string } | null;

const fieldClass =
  'w-full rounded-sm border border-edge bg-canvas px-2 py-1 text-[length:var(--text-sm)] text-ink';

/** The Add a point dialog: category, text, root cause (Went badly only), an optional theme, and whether the point is about the Sprint or one of its items. */
export function AddPointDialog({
  project,
  sprint,
  retro,
  onClose,
}: SprintRetroSlotProps & { onClose: () => void }) {
  const [category, setCategory] = useState<RetroCategory>('wentBadly');
  const [text, setText] = useState('');
  const [rootCause, setRootCause] = useState('');
  const [workItemId, setWorkItemId] = useState('');
  const [theme, setTheme] = useState<ThemeChoice>(null);
  const [error, setError] = useState<string | null>(null);
  const themes = useRetroThemes(project.id);
  const create = useCreatePoint(project.id);
  const items = subjectItems(retro);
  const cause = rootCause.trim();

  async function save() {
    setError(null);
    try {
      await create.mutateAsync({
        retroId: null,
        workItemId: workItemId === '' ? null : workItemId,
        sprintId: workItemId === '' ? sprint.id : null,
        category,
        text: text.trim(),
        rootCause: category === 'wentBadly' && cause !== '' ? cause : null,
        themeId: theme?.id ?? null,
        themeName: theme && theme.id === null ? theme.name : null,
      });
      onClose();
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'The point could not be added.');
    }
  }

  return (
    <Dialog
      title="Add a point"
      onClose={onClose}
      testId={ids.retroAddPointDialog}
      footer={
        <>
          <span className="flex-1" />
          <button type="button" className="cursor-pointer px-3 py-1 text-ink" onClick={onClose}>
            Cancel
          </button>
          <button
            type="button"
            data-testid={ids.retroAddPointSave}
            disabled={text.trim() === '' || create.isPending}
            className="cursor-pointer rounded-control bg-accent px-3 py-1 text-signal-ink disabled:cursor-default disabled:opacity-60"
            onClick={() => void save()}
          >
            Add point
          </button>
        </>
      }
    >
      <label className="grid gap-1 text-[length:var(--text-sm)]">
        <span className="text-ink-muted">Category</span>
        <select
          data-testid={ids.retroAddPointCategory}
          value={category}
          className={fieldClass}
          onChange={(e) => setCategory(e.target.value as RetroCategory)}
        >
          {RETRO_CATEGORIES.map((c) => (
            <option key={c} value={c}>
              {retroCategoryLabels[c]}
            </option>
          ))}
        </select>
      </label>
      <label className="grid gap-1 text-[length:var(--text-sm)]">
        <span className="text-ink-muted">About</span>
        <select
          data-testid={ids.retroAddPointItem}
          value={workItemId}
          className={fieldClass}
          onChange={(e) => setWorkItemId(e.target.value)}
        >
          <option value="">Sprint</option>
          {items.map((item) => (
            <option key={item.id} value={item.id}>
              {item.key} {item.title}
            </option>
          ))}
        </select>
      </label>
      <label className="grid gap-1 text-[length:var(--text-sm)]">
        <span className="text-ink-muted">Point</span>
        <textarea
          data-testid={ids.retroAddPointText}
          value={text}
          rows={3}
          className={fieldClass}
          onChange={(e) => setText(e.target.value)}
        />
      </label>
      {category === 'wentBadly' ? (
        <label className="grid gap-1 text-[length:var(--text-sm)]">
          <span className="text-ink-muted">Root cause</span>
          <textarea
            data-testid={ids.retroAddPointRootCause}
            value={rootCause}
            rows={2}
            className={fieldClass}
            onChange={(e) => setRootCause(e.target.value)}
          />
        </label>
      ) : null}
      <div className="grid gap-1 text-[length:var(--text-sm)]">
        <span className="text-ink-muted">
          Theme (optional){theme ? `: ${theme.name}` : ''}
          {theme ? (
            <button
              type="button"
              className="ml-2 cursor-pointer text-ink-muted underline"
              onClick={() => setTheme(null)}
            >
              Clear
            </button>
          ) : null}
        </span>
        {themes.data ? (
          <ThemePicker
            themes={themes.data.themes}
            mode={themes.data.mode}
            value={theme?.id ?? null}
            onPick={(pick) => {
              if (typeof pick === 'string') {
                const found = themes.data.themes.find((t) => t.theme.id === pick);
                setTheme({ id: pick, name: found?.theme.name ?? pick });
              } else {
                setTheme({ id: null, name: pick.name });
              }
            }}
          />
        ) : null}
      </div>
      {error ? (
        <p role="alert" className="m-0 text-[length:var(--text-sm)] text-alert">
          {error}
        </p>
      ) : null}
    </Dialog>
  );
}
