import type { RetroActionRefDto } from '../../../api/retros';
import { StateDot } from '../../../components/StateDot';
import { TypeIcon } from '../../../components/TypeIcon';
import { ItemLink } from '../../itemPeek/ItemLink';
import { retroSharedTestIds as ids } from '../testIds';

/** The action item a point is pinned to: a white pin on the paper with the item's type, key, state and the other points it covers. */
export function PinnedAction({ action }: { action: RetroActionRefDto }) {
  const others = action.pointCount - 1;
  return (
    <div data-testid={ids.retroNotePin} className="min-w-0">
      <ItemLink
        itemKey={action.key}
        title={action.title}
        className="flex min-w-0 flex-wrap items-center gap-[5px] rounded-[var(--radius-xs)] border border-[color-mix(in_srgb,#ffffff,#1c2638_18%)] bg-white px-1.5 py-[3px] text-[length:var(--text-xs)] text-ink no-underline shadow-[0_1px_1px_rgb(16_24_40/0.12)] hover:border-signal"
      >
        <TypeIcon type="action" />
        <span className="font-mono">{action.key}</span>
        <StateDot category={action.state.category} name={action.state.name} />
        {others > 0 ? (
          <span className="whitespace-nowrap text-[#52627c]">
            +{others} more point{others === 1 ? '' : 's'}
          </span>
        ) : null}
      </ItemLink>
    </div>
  );
}
