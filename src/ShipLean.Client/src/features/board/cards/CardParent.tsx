import type { ParentRefDto } from '../../../api/workItems';
import { TypeIcon } from '../../../components/TypeIcon';
import { ItemLink } from '../../itemPeek/ItemLink';
import { boardTestIds as ids } from '../testIds';

// The parent line: the parent's type icon, key and title (a Story's Feature, a Feature's Epic or parent Feature).
export function CardParent({ parent }: { parent: ParentRefDto }) {
  return (
    <div
      data-testid={ids.cardParent}
      data-parent-key={parent.key}
      className="flex min-w-0 flex-1 items-center gap-[5px] text-[length:var(--text-xs)] text-ink-muted"
    >
      <TypeIcon type={parent.type.id} />
      <ItemLink itemKey={parent.key} className="min-w-0 flex-1 truncate">
        {parent.key} {parent.title}
      </ItemLink>
    </div>
  );
}
