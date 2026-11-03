import type {
  RetroInsightColumnDto,
  RetroInsightsMode,
  RetroThemeInsightDto,
} from '../../../../api/retros';
import { ActionChip } from '../../../retroShared/ActionChip';
import { retrosTestIds as ids } from '../../testIds';
import { MentionCell } from './MentionCell';
import { NoSprintCell } from './NoSprintCell';
import { StatusChip } from './StatusChip';
import { ThemeActionButton } from './ThemeActionButton';
import { ThemeNameCell } from './ThemeNameCell';
import { VerdictChip } from './VerdictChip';

/** One theme: name, a mention cell per column, No sprint (sprint mode), total, status, its action items and the last change's verdict. */
export function RecurringRow({
  projectId,
  row,
  columns,
  max,
  mode,
}: {
  projectId: string;
  row: RetroThemeInsightDto;
  columns: readonly RetroInsightColumnDto[];
  max: number;
  mode: RetroInsightsMode;
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
      {columns.map((column, i) => (
        <MentionCell
          key={column.key}
          count={row.counts[i] ?? 0}
          max={max}
          columnKey={column.key}
          columnLabel={column.label}
        />
      ))}
      {mode === 'sprint' ? <NoSprintCell count={row.noSprint} /> : null}
      <td data-cell="total" className="px-3 py-2 text-right tabular-nums">
        {row.total}
      </td>
      <td className="px-3 py-2">
        <StatusChip status={row.status} mode={mode} />
      </td>
      <td className="px-3 py-2">
        <div className="flex flex-wrap gap-1.5">
          {row.actionItems.map((action) => (
            <ActionChip key={action.workItemId} action={action} />
          ))}
          <ThemeActionButton themeId={row.theme.id} themeName={row.theme.name} />
        </div>
      </td>
      <td className="px-3 py-2">
        <VerdictChip verdict={row.lastVerdict} mode={mode} />
      </td>
    </tr>
  );
}
