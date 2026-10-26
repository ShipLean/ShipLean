import type { BoardColumnDto } from '../../../api/views';
import type { BoardFilterQuery } from '../boardApi';
import { boardTestIds as ids } from '../testIds';
import { filterCount, hiddenCards } from './filterQuery';

// Said next to the summary while a filter is on: hidden cards still count toward every column's WIP.
export function FilterNote({
  columns,
  filter,
}: {
  columns: BoardColumnDto[];
  filter: BoardFilterQuery;
}) {
  if (filterCount(filter) === 0) return null;
  const hidden = hiddenCards(columns);
  return (
    <span data-testid={ids.filterNote} data-hidden={hidden} className="text-ink-muted">
      {hidden} {hidden === 1 ? 'card' : 'cards'} hidden by the filter · WIP counts include hidden
      cards
    </span>
  );
}
