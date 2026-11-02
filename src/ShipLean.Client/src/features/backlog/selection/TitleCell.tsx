import type { BacklogRowDto } from '../../../api/views';
import { TypeIcon } from '../../../components/TypeIcon';
import { ItemLink } from '../../itemPeek/ItemLink';
import { backlogTestIds as ids } from '../testIds';

// The title cell of a row: the type icon and the link to the item. Tree mode renders it too.
// TODO: the click rules (plain click opens the modal, Ctrl-click selects while a selection exists) replace this link-only cell.
export function TitleCell({ row, projectKey }: { row: BacklogRowDto; projectKey: string }) {
  return (
    <span className="inline-flex items-center gap-1.5">
      {row.type ? <TypeIcon type={row.type.id} /> : null}
      <ItemLink testId={ids.title} projectKey={projectKey} itemKey={row.key}>
        {row.title}
      </ItemLink>
    </span>
  );
}
