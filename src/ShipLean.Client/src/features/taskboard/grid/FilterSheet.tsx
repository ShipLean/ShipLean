import type { UserRef } from '@shiplean/core/api/refs';
import { Avatar } from '../../../components/Avatar';
import { actionSheetRowClass } from '../../../touch/ActionSheet';
import { Sheet } from '../../../touch/Sheet';
import { taskboardTestIds as ids } from '../testIds';
import { UNASSIGNED_FACE } from './faceFilter';

/**
 * The Filter sheet at 900px and below (SHIP-722 D6): a checkbox row per person and Unassigned with their task count on
 * this board, and "Show everyone's tasks", which clears the filter. Changes apply as they are made.
 */
export function FilterSheet({
  open,
  onClose,
  people,
  chosen,
  counts,
  onToggle,
  onClear,
}: {
  open: boolean;
  onClose: () => void;
  people: UserRef[];
  chosen: ReadonlySet<string>;
  counts: ReadonlyMap<string, number>;
  onToggle: (faceId: string) => void;
  onClear: () => void;
}) {
  const faces = [
    ...people.map((p) => ({ id: p.id, person: p })),
    { id: UNASSIGNED_FACE, person: null },
  ];
  return (
    <Sheet open={open} title="Show tasks for" onClose={onClose}>
      {faces.map(({ id, person }) => (
        <label
          key={id}
          data-testid={ids.filterSheetRow}
          data-face={id}
          className={`${actionSheetRowClass} cursor-pointer gap-3 text-ink`}
        >
          <input
            type="checkbox"
            checked={chosen.has(id)}
            className="size-4 flex-none accent-[var(--color-accent)]"
            onChange={() => onToggle(id)}
          />
          <Avatar {...(person ?? {})} />
          <span className="min-w-0 flex-1 truncate">{person?.name ?? 'Unassigned'}</span>
          <span data-testid={ids.filterSheetCount} className="text-ink-muted">
            {counts.get(id) ?? 0}
          </span>
        </label>
      ))}
      <button
        type="button"
        data-testid={ids.filterSheetClear}
        disabled={chosen.size === 0}
        className={`${actionSheetRowClass} text-signal`}
        onClick={onClear}
      >
        Show everyone&apos;s tasks
      </button>
    </Sheet>
  );
}
