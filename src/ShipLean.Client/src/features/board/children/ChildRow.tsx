import { StateDot } from '../../../components/StateDot';
import { TypeIcon } from '../../../components/TypeIcon';
import type { ChildSummaryDto } from '@shiplean/core/api/workItems';
import { useTouchShell } from '../../../touch/useTouchShell';
import { ItemLink } from '../../itemPeek/ItemLink';
import { boardTestIds as ids } from '../testIds';

// One child: type, key, title (opens the item) and its state; closed ones are muted. At touch widths, as the design's
// list inside the card: no key, and the state with its coloured dot.
export function ChildRow({ child, projectKey }: { child: ChildSummaryDto; projectKey?: string }) {
  const { touch } = useTouchShell();
  const closed = child.state.category === 'completed' || child.state.category === 'removed';
  return (
    <li
      data-testid={ids.childRow}
      data-key={child.key}
      data-closed={closed}
      className={`flex h-6 touch:h-[var(--touch-target)] min-w-0 items-center gap-1.5 rounded-sm px-1 text-[length:var(--text-xs)] hover:bg-sunken ${
        closed ? 'text-ink-muted' : ''
      }`}
    >
      <TypeIcon type={child.type.id} />
      {touch ? null : (
        <span className="text-[length:var(--text-2xs)] text-ink-muted">{child.key}</span>
      )}
      <ItemLink
        itemKey={child.key}
        projectKey={projectKey}
        className="min-w-0 flex-1 truncate touch:flex touch:h-full touch:items-center"
      >
        {child.title}
      </ItemLink>
      <span
        data-testid={ids.childState}
        className="flex-none text-[length:var(--text-2xs)] text-ink-muted"
      >
        {touch ? (
          <StateDot category={child.state.category} name={child.state.name} />
        ) : (
          child.state.name
        )}
      </span>
    </li>
  );
}
