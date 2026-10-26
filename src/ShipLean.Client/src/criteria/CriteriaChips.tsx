import type { CriteriaJson } from '../api/persisted';
import { Chip } from '../components/Chip';
import { criteriaTestIds } from '../features/criteria/testIds';
import { clauseText } from './clauseText';
import type { PickLists } from './valueOptions';

/** The clauses of the criteria as chips, for a toolbar: "Type = Bug", then "and" / "or" in front of the rest. Pass `onRemove` to let each chip drop its clause. */
export function CriteriaChips({
  value,
  onRemove,
  ...lists
}: PickLists & {
  value: CriteriaJson;
  onRemove?: (index: number) => void;
}) {
  const joiner = value.match === 'all' ? 'and' : 'or';
  return (
    <span data-testid={criteriaTestIds.chips} className="inline-flex flex-wrap items-center gap-1">
      {value.clauses.map((clause, index) => (
        <Chip
          // A clause has no identity of its own; its place in the list is it.
          key={index}
          tone="accent"
          onRemove={onRemove ? () => onRemove(index) : undefined}
        >
          {index > 0 ? `${joiner} ` : ''}
          {clauseText(clause, lists)}
        </Chip>
      ))}
    </span>
  );
}
