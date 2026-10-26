import type { CriteriaJson } from '../../../api/persisted';
import { CriteriaChips } from '../../../criteria/CriteriaChips';
import type { PickLists } from '../../../criteria/valueOptions';
import { backlogTestIds as ids } from '../testIds';

// The toolbar's filter: the Filter button (with its clause count) and the active clauses as chips, each removable.
export function FilterButton({
  criteria,
  open,
  onToggle,
  onRemoveClause,
  lists,
}: {
  criteria: CriteriaJson;
  open: boolean;
  onToggle: () => void;
  onRemoveClause: (index: number) => void;
  lists: PickLists;
}) {
  const count = criteria.clauses.length;
  return (
    <>
      <button
        type="button"
        data-testid={ids.filterButton}
        aria-pressed={open}
        aria-expanded={open}
        className="btn-ghost aria-pressed:bg-signal-soft"
        onClick={onToggle}
      >
        {count > 0 ? `Filter (${count})` : 'Filter'}
      </button>
      {count > 0 ? <CriteriaChips value={criteria} onRemove={onRemoveClause} {...lists} /> : null}
    </>
  );
}
