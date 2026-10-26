import { useState } from 'react';
import type { ChecklistItem } from '../../../api/persisted';
import { Chip } from '../../../components/Chip';
import { workItemTestIds as ids } from '../testIds';
import { inputClass } from './inputClasses';

// A Story's acceptance criteria as a checklist with an `n/m met` chip.
export function CriteriaSection({
  value,
  changed,
  onChange,
}: {
  value: ChecklistItem[];
  changed: boolean;
  onChange: (criteria: ChecklistItem[]) => void;
}) {
  const [text, setText] = useState('');
  const met = value.filter((c) => c.done).length;
  const add = () => {
    const next = text.trim();
    if (next === '') return;
    setText('');
    onChange([...value, { text: next, done: false }]);
  };
  return (
    <section data-testid={ids.criteria} data-field="acceptanceCriteria" className="grid gap-2">
      <header className="flex items-center gap-2">
        <h2 className="flex items-center gap-1 text-[length:var(--text-md)] font-semibold">
          Acceptance criteria
          {changed ? (
            <span
              role="img"
              aria-label="Unsaved change"
              className="size-1.5 rounded-full bg-signal"
            />
          ) : null}
        </h2>
        <span data-testid={ids.criteriaMet}>
          <Chip tone={value.length > 0 && met === value.length ? 'ok' : 'muted'}>
            {met}/{value.length} met
          </Chip>
        </span>
      </header>
      <ul className="grid gap-1">
        {value.map((criterion, index) => (
          <li key={index} className="flex items-center gap-2">
            <label className="flex flex-1 items-center gap-2 text-[length:var(--text-base)]">
              <input
                type="checkbox"
                data-testid={ids.criterion}
                checked={criterion.done}
                onChange={() =>
                  onChange(value.map((c, i) => (i === index ? { ...c, done: !c.done } : c)))
                }
              />
              <span className={criterion.done ? 'text-ink-muted line-through' : ''}>
                {criterion.text}
              </span>
            </label>
            <button
              type="button"
              data-testid={ids.criterionRemove}
              aria-label="Remove criterion"
              className="text-ink-muted hover:text-ink"
              onClick={() => onChange(value.filter((_, i) => i !== index))}
            >
              ×
            </button>
          </li>
        ))}
      </ul>
      <input
        data-testid={ids.criterionInput}
        aria-label="Add a criterion"
        placeholder="+ Add a criterion, then Enter"
        autoComplete="off"
        className={inputClass}
        value={text}
        onChange={(event) => setText(event.target.value)}
        onKeyDown={(event) => {
          if (event.key === 'Enter') {
            event.preventDefault();
            add();
          }
        }}
      />
    </section>
  );
}
