import type { CriteriaJson } from '@shiplean/core/api/persisted';
import { CriteriaBuilder } from '../../../criteria/CriteriaBuilder';
import type { PickLists } from '../../../criteria/valueOptions';
import { Sheet } from '../../../touch/Sheet';
import { backlogTestIds as ids } from '../testIds';

// The filter at 900px and below: a bottom sheet with the clauses stacked. A change applies as it is made (there is no Apply);
// Close just puts the sheet away, and the chips under the toolbar show what is on.
export function FilterSheet({
  criteria,
  onChange,
  error,
  lists,
  onClose,
  count,
}: {
  criteria: CriteriaJson;
  onChange: (criteria: CriteriaJson) => void;
  // The server's message for a filter it refused (400), or null.
  error: string | null;
  lists: PickLists;
  onClose: () => void;
  // The items shown and the items the list holds with no filter; no footer count while the whole is unread.
  count?: { shown: number; whole: number };
}) {
  return (
    <Sheet open title="Filter" onClose={onClose}>
      <div data-testid={ids.filterSheet} className="grid gap-3 pb-1">
        <CriteriaBuilder
          value={criteria}
          onChange={onChange}
          showChips={false}
          layout="sheet"
          {...lists}
        />
        {error ? (
          <p
            role="alert"
            data-testid={ids.filterError}
            className="text-[length:var(--text-sm)] text-alert"
          >
            {error}
          </p>
        ) : null}
        <div className="flex items-center gap-3 border-t border-edge pt-2 text-[length:var(--text-sm)] text-ink-muted">
          <span data-testid={ids.filterCount} className="min-w-0 flex-1">
            {count
              ? `Showing ${count.shown} of ${count.whole} · applies as you change it`
              : 'Applies as you change it'}
          </span>
          {criteria.clauses.length > 0 ? (
            <button
              type="button"
              data-testid={ids.filterClear}
              className="flex-none rounded-control px-2 text-[length:var(--text-base)] font-medium text-signal touch:min-h-[var(--touch-target)]"
              onClick={() => onChange({ ...criteria, clauses: [] })}
            >
              Clear
            </button>
          ) : null}
        </div>
      </div>
    </Sheet>
  );
}
