import type { CriteriaJson } from '@shiplean/core/api/persisted';
import { clauseText } from '../../../criteria/clauseText';
import type { PickLists } from '../../../criteria/valueOptions';
import { backlogTestIds as ids } from '../testIds';

// The active clauses as chips under the touch toolbar (900px and below). Each chip is one button: pressing it removes the
// clause, and it is as tall as a touch target (the shared Chip's × is too small to press).
export function FilterChips({
  criteria,
  onRemoveClause,
  lists,
}: {
  criteria: CriteriaJson;
  onRemoveClause: (index: number) => void;
  lists: PickLists;
}) {
  const joiner = criteria.match === 'all' ? 'and' : 'or';
  return (
    <>
      {criteria.clauses.map((clause, index) => {
        const text = `${index > 0 ? `${joiner} ` : ''}${clauseText(clause, lists)}`;
        return (
          <button
            // A clause has no identity of its own; its place in the list is it.
            key={index}
            type="button"
            data-testid={ids.filterChip}
            aria-label={`Remove filter: ${text}`}
            className="inline-flex min-h-[var(--touch-target)] max-w-full items-center gap-2 rounded-full bg-accent-soft px-3 text-[length:var(--text-sm)] font-medium text-ink"
            onClick={() => onRemoveClause(index)}
          >
            <span className="min-w-0 truncate">{text}</span>
            <span aria-hidden="true" className="text-ink-muted">
              ✕
            </span>
          </button>
        );
      })}
    </>
  );
}
