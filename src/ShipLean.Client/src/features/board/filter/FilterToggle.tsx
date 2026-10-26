import type { BoardFilterQuery } from '../boardApi';
import { boardTestIds as ids } from '../testIds';
import { filterCount } from './filterQuery';

// The Filter button: opens and closes the filter bar, and says how many parts of the filter are set.
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
      className="btn-ghost"
      aria-expanded={open}
      onClick={onToggle}
    >
      {count === 0 ? 'Filter' : `Filter (${count})`}
    </button>
  );
}
