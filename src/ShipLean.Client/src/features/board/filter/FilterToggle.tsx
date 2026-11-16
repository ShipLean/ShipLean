import type { BoardFilterQuery } from '../boardApi';
import { boardTestIds as ids } from '../testIds';
import { filterCount } from './filterQuery';

// The Filter button: opens and closes the filter bar, and says how many parts of the filter are set; at touch widths a filter that is on gives it the accent border.
export function FilterToggle({
  filter,
  open,
  onToggle,
}: {
  filter: BoardFilterQuery;
  open: boolean;
  onToggle: () => void;
}) {
  const count = filterCount(filter);
  return (
    <button
      type="button"
      data-testid={ids.filterToggle}
      className={`btn-ghost touch:min-h-[var(--touch-target)] touch:flex-none${count > 0 ? ' touch:border touch:border-signal touch:bg-signal-soft touch:text-ink' : ''}`}
      aria-expanded={open}
      onClick={onToggle}
    >
      {count === 0 ? 'Filter' : `Filter (${count})`}
    </button>
  );
}
