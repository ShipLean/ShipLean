import { useState } from 'react';
import type { StateCategory } from '../../../api/refs';
import { settingsTestIds } from '../testIds';
import { CATEGORIES, CATEGORY_LABELS, parseCategory } from './categories';

const field =
  'h-7 rounded-control border border-edge bg-panel px-2 text-[length:var(--text-sm)] text-ink';

/** Name and category of a new state of the shown type; it joins the draft at the end of the list. */
export function AddStateForm({
  taken,
  onAdd,
}: {
  taken(name: string): boolean;
  onAdd(name: string, category: StateCategory): void;
}) {
  const [name, setName] = useState('');
  const [category, setCategory] = useState<StateCategory>('inProgress');
  const trimmed = name.trim();
  const repeated = trimmed !== '' && taken(trimmed);
  return (
    <div className="flex items-center gap-2">
      <input
        data-testid={settingsTestIds.stateAddName}
        aria-label="New state name"
        className={field}
        value={name}
        maxLength={30}
        placeholder="Name the state"
        autoComplete="off"
        onChange={(e) => setName(e.target.value)}
      />
      <select
        data-testid={settingsTestIds.stateAddCategory}
        aria-label="New state category"
        className={field}
        value={category}
        onChange={(e) => setCategory(parseCategory(e.target.value))}
      >
        {CATEGORIES.map((c) => (
          <option key={c} value={c}>
            {CATEGORY_LABELS[c]}
          </option>
        ))}
      </select>
      <button
        type="button"
        data-testid={settingsTestIds.stateAdd}
        disabled={trimmed === '' || repeated}
        className="h-7 rounded-control border border-edge px-2 text-[length:var(--text-sm)] text-ink disabled:opacity-60"
        onClick={() => {
          onAdd(trimmed, category);
          setName('');
        }}
      >
        + Add state
      </button>
      {repeated ? (
        <span className="text-[length:var(--text-sm)] text-alert">
          Another state is already called {trimmed}.
        </span>
      ) : null}
    </div>
  );
}
