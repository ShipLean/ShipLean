import type { RetroActionRowDto, RetroInsightsMode } from '@shiplean/core/api/retros';
import type { SprintDto } from '@shiplean/core/api/sprints';
import { Avatar } from '../../../../components/Avatar';
import { Chip } from '../../../../components/Chip';
import { StateDot } from '../../../../components/StateDot';
import { TypeIcon } from '../../../../components/TypeIcon';
import { ItemLink } from '../../../itemPeek/ItemLink';
import { retroFixOptions } from '../../../retroShared/actionDialog/retroFixTypes';
import { retrosTestIds as ids } from '../../testIds';
import { PullButton } from '../actions/PullButton';
import { VerdictChip } from '../recurring/VerdictChip';

/** The fix type's label, or undefined when the action has none. */
const fixLabel = (row: RetroActionRowDto) =>
  row.action.fixType ? retroFixOptions.find((o) => o.id === row.action.fixType)?.label : undefined;

/**
 * One action item on a phone: key, state and points; title (the whole card opens the item through the title's link);
 * theme; fix, covers and raised; then the sprint or the pull button, the owner and the impact.
 */
export function ActionItemCard({
  projectId,
  row,
  next,
  recurring,
  mode,
}: {
  projectId: string;
  row: RetroActionRowDto;
  next: SprintDto | undefined;
  recurring: boolean;
  mode: RetroInsightsMode;
}) {
  const { action, owner, theme } = row;
  const open = action.state.category !== 'completed' && action.state.category !== 'removed';
  const details = [
    fixLabel(row) ? `Fix: ${fixLabel(row)}` : null,
    `Covers ${action.pointCount}`,
    row.raisedIn ? `Raised ${row.raisedIn.name}` : null,
  ].filter((part) => part !== null);
  return (
    <article
      data-testid={ids.retroActionCard}
      data-item-key={action.key}
      className="relative grid gap-1 rounded-panel border border-edge bg-panel px-3 py-2 text-[length:var(--text-sm)]"
    >
      <div className="flex items-center gap-2">
        <TypeIcon type="action" />
        <span className="font-mono text-[length:var(--text-xs)] text-ink-muted">{action.key}</span>
        <StateDot category={action.state.category} name={action.state.name} />
        <span className="flex-1" />
        <span className="text-[length:var(--text-xs)] text-ink-muted tabular-nums">
          {action.points === null ? '— pts' : `${action.points} pts`}
        </span>
      </div>
      <ItemLink
        itemKey={action.key}
        testId={ids.retroActionCardLink}
        className="flex min-h-[var(--touch-target)] items-center font-semibold text-ink no-underline after:absolute after:inset-0 after:content-['']"
      >
        {action.title}
      </ItemLink>
      {theme ? (
        <div className="flex items-center gap-1.5">
          <span className="text-ink-muted">#</span>
          {theme.name}
          {recurring ? (
            <span data-recurring="">
              <Chip tone="warn">↻</Chip>
            </span>
          ) : null}
        </div>
      ) : null}
      <div className="text-[length:var(--text-xs)] text-ink-muted">{details.join(' · ')}</div>
      <div className="flex min-h-[var(--touch-target)] flex-wrap items-center gap-x-3 gap-y-1">
        {action.sprint ? (
          <span data-testid={ids.retroActionCardSprint} data-cell="sprint">
            {action.sprint.name}
          </span>
        ) : open && next ? (
          <PullButton projectId={projectId} itemKey={action.key} sprint={next} touch />
        ) : null}
        {owner ? (
          <span data-cell="owner" className="inline-flex items-center gap-1.5">
            <Avatar {...owner} />
            {owner.name}
          </span>
        ) : null}
        <span className="flex-1" />
        <span data-cell="impact" className="relative">
          {row.impact ? <VerdictChip verdict={row.impact} mode={mode} /> : null}
        </span>
      </div>
    </article>
  );
}
