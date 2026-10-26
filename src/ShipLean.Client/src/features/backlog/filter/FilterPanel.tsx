import type { CriteriaJson } from '../../../api/persisted';
import { CriteriaBuilder } from '../../../criteria/CriteriaBuilder';
import type { PickLists } from '../../../criteria/valueOptions';
import { backlogTestIds as ids } from '../testIds';

// The filter's panel, full width under the toolbar: the criteria builder, Clear, and the server's message when it refuses the filter.
export function FilterPanel({
  criteria,
  onChange,
  error,
  lists,
}: {
  criteria: CriteriaJson;
  onChange: (criteria: CriteriaJson) => void;
  // The server's message for a filter it refused (400), or null.
  error: string | null;
  lists: PickLists;
}) {
  return (
    <div
      data-testid={ids.filterPanel}
      className="flex-none border-b border-edge bg-panel px-[var(--space-4)] py-[var(--space-3)]"
    >
      <div className="mb-2 flex items-center gap-2">
        <b className="text-[length:var(--text-sm)]">Filter</b>
        <button
          type="button"
          data-testid={ids.filterClear}
          className="btn-ghost ml-auto"
          onClick={() => onChange({ ...criteria, clauses: [] })}
        >
          Clear
        </button>
      </div>
      <CriteriaBuilder value={criteria} onChange={onChange} showChips={false} {...lists} />
      {error ? (
        <p
          role="alert"
          data-testid={ids.filterError}
          className="mt-2 text-[length:var(--text-xs)] text-alert"
        >
          {error}
        </p>
      ) : null}
    </div>
  );
}
