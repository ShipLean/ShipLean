import type { ChildSummaryDto } from '@shiplean/core/api/workItems';
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
      className="flex items-center gap-2 border-t border-edge px-2 py-1.5 text-[length:var(--text-sm)] first:border-t-0 touch:flex-wrap touch:items-start touch:gap-x-3 touch:gap-y-1 touch:px-3 touch:py-2 touch:[&>:last-child]:order-4"
    >
      <TypeIcon type={child.type.id} />
      <span
        data-testid={ids.childKey}
        className="w-20 shrink-0 text-ink-muted touch:order-2 touch:ml-7 touch:w-auto"
      >
        {child.key}
      </span>
      <ItemLink
        testId={ids.childTitle}
        projectKey={projectKey}
        itemKey={child.key}
        className="min-w-0 flex-1 truncate hover:text-signal hover:underline touch:order-1 touch:inline-flex touch:min-h-8 touch:basis-[calc(100%-2.5rem)] touch:items-center touch:whitespace-normal touch:break-words"
      >
        {child.title}
      </ItemLink>
      <span
        data-testid={ids.childState}
        className="w-24 shrink-0 text-ink-muted touch:order-3 touch:w-auto"
      >
        {child.state.name}
      </span>
      <Avatar {...(child.assignee ?? {})} />
    </li>
  );
}
