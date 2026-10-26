import { TypeIcon } from '../../../components/TypeIcon';
import type { ChildSummaryDto } from '../../../api/workItems';
import { ItemLink } from '../../itemPeek/ItemLink';
import { boardTestIds as ids } from '../testIds';

// One child: type, key, title (opens the item) and its state; closed ones are muted.
export function ChildRow({ child, projectKey }: { child: ChildSummaryDto; projectKey?: string }) {
  const closed = child.state.category === 'completed' || child.state.category === 'removed';
  return (
    <li
      data-testid={ids.childRow}
      data-key={child.key}
      data-closed={closed}
      className={`flex h-6 min-w-0 items-center gap-1.5 rounded-sm px-1 text-[length:var(--text-xs)] hover:bg-sunken ${
        closed ? 'text-ink-muted' : ''
      }`}
    >
      <TypeIcon type={child.type.id} />
      <span className="text-[length:var(--text-2xs)] text-ink-muted">{child.key}</span>
      <ItemLink itemKey={child.key} projectKey={projectKey} className="min-w-0 flex-1 truncate">
        {child.title}
      </ItemLink>
      <span data-testid={ids.childState} className="text-[length:var(--text-2xs)] text-ink-muted">
        {child.state.name}
      </span>
    </li>
  );
}
