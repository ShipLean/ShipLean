import type { ChildSummaryDto } from '../../../api/workItems';
import { Avatar } from '../../../components/Avatar';
import { TypeIcon } from '../../../components/TypeIcon';
import { ItemLink } from '../../itemPeek/ItemLink';
import { featureTestIds as ids } from '../testIds';

// One story or bug: type icon, key, title (opens it), state and assignee. Each is its own work item.
export function ChildRow({ child, projectKey }: { child: ChildSummaryDto; projectKey: string }) {
  return (
    <li
      data-testid={ids.childRow}
      data-key={child.key}
      className="flex items-center gap-2 border-t border-edge px-2 py-1.5 text-[length:var(--text-sm)] first:border-t-0"
    >
      <TypeIcon type={child.type.id} />
      <span data-testid={ids.childKey} className="w-20 shrink-0 text-ink-muted">
        {child.key}
      </span>
      <ItemLink
        testId={ids.childTitle}
        projectKey={projectKey}
        itemKey={child.key}
        className="min-w-0 flex-1 truncate hover:text-signal hover:underline"
      >
        {child.title}
      </ItemLink>
      <span data-testid={ids.childState} className="w-24 shrink-0 text-ink-muted">
        {child.state.name}
      </span>
      <Avatar {...(child.assignee ?? {})} />
    </li>
  );
}
