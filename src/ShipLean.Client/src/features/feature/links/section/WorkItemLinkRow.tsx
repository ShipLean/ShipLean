import type { WorkItemLinkRowDto } from '../../../../api/linkCards';
import { TypeIcon } from '../../../../components/TypeIcon';
import { ItemLink } from '../../../itemPeek/ItemLink';
import { linkTypeLabels } from '../../../workItem/links/linkTypes';
import { featureTestIds as ids } from '../../testIds';

// One linked work item: type icon, key, title (opens it) and how it is linked.
export function WorkItemLinkRow({
  row,
  projectKey,
}: {
  row: WorkItemLinkRowDto;
  projectKey: string;
}) {
  return (
    <li
      data-testid={ids.linkWorkItemRow}
      data-key={row.key}
      className="flex items-center gap-2 border-t border-edge px-2 py-1.5 text-[length:var(--text-sm)] first:border-t-0"
    >
      <TypeIcon type={row.itemType.id} />
      <span className="w-20 shrink-0 text-ink-muted">{row.key}</span>
      <ItemLink
        projectKey={projectKey}
        itemKey={row.key}
        className="min-w-0 flex-1 truncate hover:text-signal hover:underline"
      >
        {row.title}
      </ItemLink>
      <span className="shrink-0 text-ink-muted">{linkTypeLabels[row.type]}</span>
    </li>
  );
}
