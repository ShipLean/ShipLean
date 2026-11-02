import { backlogTestIds as ids } from '../testIds';

// The head cell over the select column.
// TODO: the select-all checkbox replaces this empty head cell.
export function SelectAllCell() {
  return <th data-testid={ids.selectAll} className="w-8 px-1" />;
}
