import type { UserRef } from '../../../api/refs';
import { boardTestIds as ids } from '../testIds';
import { NONE } from './filterQuery';

// People chips, any of several: the project's members and the "no one" chip (Unassigned on Stories, No owner above).
export function PeopleFilter({
  label,
  noneLabel,
  people,
  chosen,
  onToggle,
}: {
  label: string;
  noneLabel: string;
  people: UserRef[];
  chosen: string[];
  onToggle: (id: string) => void;
}) {
  const options = [
    ...people.map((person) => ({ id: person.id, name: person.name })),
    { id: NONE, name: noneLabel },
  ];
  return (
    <span role="group" aria-label={label} className="flex flex-wrap items-center gap-1">
      {options.map((option) => (
        <button
          key={option.id}
          type="button"
          data-testid={ids.filterPerson}
          data-person={option.id}
          aria-pressed={chosen.includes(option.id)}
          className={`h-7 rounded-full border px-2.5 text-[length:var(--text-xs)] ${
            chosen.includes(option.id)
              ? 'border-signal bg-signal-soft text-ink'
              : 'border-edge text-ink-muted hover:text-ink'
          }`}
          onClick={() => onToggle(option.id)}
        >
          {option.name}
        </button>
      ))}
    </span>
  );
}
