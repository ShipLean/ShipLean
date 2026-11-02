import { Avatar } from '../../../../components/Avatar';
import { StateDot } from '../../../../components/StateDot';
import { TypeIcon } from '../../../../components/TypeIcon';
import type { RetroActionRowDto, RetroInsightsMode } from '../../../../api/retros';
import type { SprintDto } from '../../../../api/sprints';
import { Chip } from '../../../../components/Chip';
import { ItemLink } from '../../../itemPeek/ItemLink';
import { retrosTestIds as ids } from '../../testIds';
import { VerdictChip } from '../recurring/VerdictChip';
import { PullButton } from './PullButton';

const NONE = <span className="text-ink-muted">—</span>;

/** One action item: key and title, theme, sprint raised, state, sprint (or the pull button), points, owner and impact. */
export function ActionRow({
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
  const { action, owner } = row;
  const open = action.state.category !== 'completed' && action.state.category !== 'removed';
  return (
    <tr
      data-testid={ids.retroActionRow}
      data-item-key={action.key}
      className="border-t border-edge text-[length:var(--text-sm)]"
    >
      <td className="px-3 py-2">
        <ItemLink
          itemKey={action.key}
          className="inline-flex items-center gap-1.5 text-ink no-underline"
        >
          <TypeIcon type="action" />
          <span className="font-mono text-[length:var(--text-xs)] whitespace-nowrap text-ink-muted">
            {action.key}
          </span>
          <span>{action.title}</span>
        </ItemLink>
      </td>
      <td data-cell="theme" className="px-3 py-2">
        {row.theme ? (
          <span className="inline-flex items-center gap-1.5">
            {row.theme.name}
            {recurring ? (
              <span data-recurring="">
                <Chip tone="warn">↻</Chip>
              </span>
            ) : null}
          </span>
        ) : (
          NONE
        )}
      </td>
      <td data-cell="raised" className="px-3 py-2">
        {row.raisedIn?.name ?? NONE}
      </td>
      <td data-cell="state" className="px-3 py-2">
        <StateDot category={action.state.category} name={action.state.name} />
      </td>
      <td data-cell="sprint" className="px-3 py-2">
        {action.sprint ? (
          action.sprint.name
        ) : open && next ? (
          <PullButton projectId={projectId} itemKey={action.key} sprint={next} />
        ) : (
          NONE
        )}
      </td>
      <td data-cell="points" className="px-3 py-2 text-right tabular-nums">
        {action.points ?? NONE}
      </td>
      <td data-cell="owner" className="px-3 py-2">
        {owner ? (
          <span className="inline-flex items-center gap-1.5">
            <Avatar {...owner} />
            {owner.name}
          </span>
        ) : (
          NONE
        )}
      </td>
      <td data-cell="impact" className="px-3 py-2">
        {row.impact ? <VerdictChip verdict={row.impact} mode={mode} /> : NONE}
      </td>
    </tr>
  );
}
