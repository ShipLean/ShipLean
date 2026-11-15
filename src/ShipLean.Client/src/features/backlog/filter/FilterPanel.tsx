import type { CriteriaJson } from '@shiplean/core/api/persisted';
import { CriteriaBuilder } from '../../../criteria/CriteriaBuilder';
import type { PickLists } from '../../../criteria/valueOptions';
import { useTouchShell } from '../../../touch/useTouchShell';
import { backlogTestIds as ids } from '../testIds';
import { FilterSheet } from './FilterSheet';

// The filter's panel, full width under the toolbar: the criteria builder, Clear, and the server's message when it refuses the filter.
export function FilterPanel({
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
  // Puts the panel away. At 900px and below it is what lets the filter be a bottom sheet; without it the panel stays inline.
  onClose?: () => void;
}) {
  const { touch } = useTouchShell();
  if (touch && onClose) {
    return (
      <FilterSheet
        criteria={criteria}
        onChange={onChange}
        error={error}
        lists={lists}
        onClose={onClose}
      />
    );
  }
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
