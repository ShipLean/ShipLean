import type { MouseEvent } from 'react';
import type { BacklogRowDto } from '../../../api/views';
import { TypeIcon } from '../../../components/TypeIcon';
import { ItemLink } from '../../itemPeek/ItemLink';
import { QueueDoneMark } from '../strip/QueueDoneMark';
import { backlogTestIds as ids } from '../testIds';
import { useSelection } from './useSelection';

// The title cell of a row: the type icon and the link to the item. A plain click opens the item (ItemLink's rule) and
// Ctrl/Cmd-click selects the row while a selection exists; without one it falls through to a new tab. Tree mode
// renders it too, where the selection is inert and nothing changes.
export function TitleCell({ row, projectKey }: { row: BacklogRowDto; projectKey: string }) {
  const selection = useSelection();
  function click(event: MouseEvent<HTMLAnchorElement>) {
    if (selection.count === 0 || event.button !== 0 || !(event.ctrlKey || event.metaKey)) return;
    event.preventDefault();
    selection.setFocused(row.id);
    selection.toggle(row.id);
  }
  return (
    <span className="inline-flex items-center gap-1.5">
      {row.type ? <TypeIcon type={row.type.id} /> : null}
      <ItemLink testId={ids.title} projectKey={projectKey} itemKey={row.key} onClick={click}>
        {row.title}
      </ItemLink>
      <QueueDoneMark rowId={row.id} />
    </span>
  );
}
