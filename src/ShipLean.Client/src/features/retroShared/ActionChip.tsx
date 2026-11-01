import { StateDot } from '../../components/StateDot';
import { TypeIcon } from '../../components/TypeIcon';
import type { RetroActionRefDto } from '../../api/retros';
import { ItemLink } from '../itemPeek/ItemLink';

/** A point's action item (found by the item link's test id and `data-item-key`): type icon, key and state, as a link to the work item (the design's `.act-chip`). */
export function ActionChip({ action }: { action: RetroActionRefDto }) {
  return (
    <ItemLink
      itemKey={action.key}
      title={action.title}
      className="inline-flex h-5 items-center gap-1 rounded-[var(--radius-xs)] border border-edge bg-panel px-1.5 whitespace-nowrap text-ink no-underline"
    >
      <TypeIcon type="action" />
      <span className="font-mono">{action.key}</span>
      <StateDot category={action.state.category} name={action.state.name} />
    </ItemLink>
  );
}
