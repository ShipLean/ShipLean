import type { SprintRef } from '../../../../api/refs';
import type { RetroThemeInsightDto } from '../../../../api/retros';
import { ActionChip } from '../../../retroShared/ActionChip';
import { retrosTestIds as ids } from '../../testIds';
import { MentionCell } from './MentionCell';
import { StatusChip } from './StatusChip';
import { ThemeNameCell } from './ThemeNameCell';
import { VerdictChip } from './VerdictChip';

/** One theme: name, a mention cell per sprint, total, status, its action items and the last change's verdict. */
export function RecurringRow({
  projectId,
  row,
  sprints,
  max,
}: {
  projectId: string;
  row: RetroThemeInsightDto;
  sprints: readonly SprintRef[];
  max: number;
}) {
  return (
    <tr
      data-testid={ids.retroRecurringRow}
      data-theme-id={row.theme.id}
      data-status={row.status}
      className="border-t border-edge"
    >
      <td className="px-3 py-2">
        <ThemeNameCell projectId={projectId} theme={row.theme} />
      </td>
      {sprints.map((sprint, i) => (
        <MentionCell
          key={sprint.id}
          count={row.counts[i] ?? 0}
          max={max}
          sprintId={sprint.id}
          sprintName={sprint.name}
        />
      ))}
      <td data-cell="total" className="px-3 py-2 text-right tabular-nums">
        {row.total}
      </td>
      <td className="px-3 py-2">
        <StatusChip status={row.status} />
      </td>
      <td className="px-3 py-2">
        <div className="flex flex-wrap gap-1.5">
          {row.actionItems.map((action) => (
            <ActionChip key={action.workItemId} action={action} />
          ))}
        </div>
      </td>
      <td className="px-3 py-2">
        <VerdictChip verdict={row.lastVerdict} />
      </td>
    </tr>
  );
}
