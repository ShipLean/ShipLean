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
}: {
  criteria: CriteriaJson;
  onChange: (criteria: CriteriaJson) => void;
  // The server's message for a filter it refused (400), or null.
  error: string | null;
  lists: PickLists;
  onClose: () => void;
}) {
  return (
    <Sheet open title="Filter" onClose={onClose}>
      <div data-testid={ids.filterSheet} className="grid gap-3 pb-1">
        <CriteriaBuilder value={criteria} onChange={onChange} showChips={false} {...lists} />
        {error ? (
          <p
            role="alert"
            data-testid={ids.filterError}
            className="text-[length:var(--text-sm)] text-alert"
          >
            {error}
          </p>
        ) : null}
        {criteria.clauses.length > 0 ? (
          <button
            type="button"
            data-testid={ids.filterClear}
            className="btn-ghost justify-self-start touch:min-h-[var(--touch-target)]"
            onClick={() => onChange({ ...criteria, clauses: [] })}
          >
            Clear
          </button>
        ) : null}
      </div>
    </Sheet>
  );
}
