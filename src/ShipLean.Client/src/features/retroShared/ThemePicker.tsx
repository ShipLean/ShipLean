import { useId, useState } from 'react';
import type { KeyboardEvent } from 'react';
import type { RetroThemeMode, RetroThemeOptionDto } from '@shiplean/core/api/retros';
import { retroSharedTestIds as ids } from './testIds';

/** Pick a theme from the project's list or add a new name. Typing filters the list case-insensitively; a name that matches none offers "Add 'X'". */
export function ThemePicker({
  themes,
  mode,
  value,
  onPick,
}: {
  themes: RetroThemeOptionDto[];
  mode: RetroThemeMode;
  /** The point's current theme id, if any. */
  value: string | null;
  onPick: (pick: string | { name: string }) => void;
}) {
  const listId = useId();
  const [query, setQuery] = useState('');
  const [active, setActive] = useState(0);
  const typed = query.trim();
  const needle = typed.toLowerCase();
  const matches = themes.filter((t) => t.theme.name.toLowerCase().includes(needle));
  const canAdd = typed !== '' && !themes.some((t) => t.theme.name.toLowerCase() === needle);
  const optionCount = matches.length + (canAdd ? 1 : 0);
  const current = themes.find((t) => t.theme.id === value);

  function choose(index: number) {
    if (index < matches.length) onPick(matches[index].theme.id);
    else if (canAdd) onPick({ name: typed });
    else return;
    setQuery('');
    setActive(0);
  }

  function onKeyDown(e: KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      setActive(optionCount === 0 ? 0 : (active + 1) % optionCount);
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setActive(optionCount === 0 ? 0 : (active + optionCount - 1) % optionCount);
    } else if (e.key === 'Enter') {
      e.preventDefault();
      choose(active);
    } else if (e.key === 'Escape') {
      setQuery('');
      setActive(0);
    }
  }

  const optionClass = (i: number) =>
    `flex cursor-pointer items-center justify-between gap-2 rounded-xs px-2 py-1 text-[length:var(--text-sm)] ${i === active ? 'bg-signal-soft' : ''}`;

  return (
    <div
      data-testid={ids.retroThemePicker}
      data-mode={mode}
      className="grid w-64 gap-1 rounded-md border border-edge bg-panel p-1.5"
    >
      <input
        type="text"
        role="combobox"
        aria-label="Theme"
        aria-expanded="true"
        aria-controls={listId}
        aria-activedescendant={optionCount > 0 ? `${listId}-${active}` : undefined}
        placeholder={current ? current.theme.name : 'Pick or add a theme'}
        value={query}
        className="h-7 rounded-sm border border-edge bg-canvas px-2 text-[length:var(--text-sm)] text-ink"
        onChange={(e) => {
          setQuery(e.target.value);
          setActive(0);
        }}
        onKeyDown={onKeyDown}
      />
      <ul id={listId} role="listbox" aria-label="Themes" className="m-0 grid list-none gap-0.5 p-0">
        {matches.map((t, i) => (
          <li
            key={t.theme.id}
            id={`${listId}-${i}`}
            role="option"
            aria-selected={t.theme.id === value}
            className={optionClass(i)}
            onMouseEnter={() => setActive(i)}
            onClick={() => choose(i)}
          >
            <span>{t.theme.name}</span>
            <span className="text-[length:var(--text-xs)] text-ink-muted">{t.pointCount}</span>
          </li>
        ))}
        {canAdd ? (
          <li
            id={`${listId}-${matches.length}`}
            role="option"
            aria-selected={false}
            data-testid={ids.retroThemePickerAdd}
            className={optionClass(matches.length)}
            onMouseEnter={() => setActive(matches.length)}
            onClick={() => choose(matches.length)}
          >
            Add &lsquo;{typed}&rsquo;
          </li>
        ) : null}
      </ul>
    </div>
  );
}
