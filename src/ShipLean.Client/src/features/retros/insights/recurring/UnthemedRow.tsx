import { Link } from 'react-router-dom';
import type {
  RetroInsightColumnDto,
  RetroInsightsMode,
  RetroUnthemedDto,
} from '../../../../api/retros';
import { routePath } from '../../../../routes';
import { retrosTestIds as ids } from '../../testIds';
import { UnthemedTip } from '../tips/UnthemedTip';
import { MentionCell } from './MentionCell';
import { NoSprintCell } from './NoSprintCell';

/** The last row: points with no theme, counted like a theme but not judged. Gives a way to Search for them. */
export function UnthemedRow({
  projectKey,
  unthemed,
  columns,
  max,
  mode,
}: {
  projectKey: string;
  unthemed: RetroUnthemedDto;
  columns: readonly RetroInsightColumnDto[];
  max: number;
  mode: RetroInsightsMode;
}) {
  if (unthemed.total === 0) return null;
  return (
    <tr
      data-testid={ids.retroInsightsUnthemed}
      className="border-t-2 border-dashed border-edge bg-canvas"
    >
      <td className="px-3 py-2">
        <span className="text-[length:var(--text-sm)] italic">Unthemed</span>
        <UnthemedTip />
      </td>
      {columns.map((column, i) => (
        <MentionCell
          key={column.key}
          count={unthemed.counts[i] ?? 0}
          max={max}
          columnKey={column.key}
          columnLabel={column.label}
        />
      ))}
      {mode === 'sprint' ? <NoSprintCell count={unthemed.noSprint} /> : null}
      <td data-cell="total" className="px-3 py-2 text-right font-semibold tabular-nums">
        {unthemed.total}
      </td>
      <td className="px-3 py-2 text-[length:var(--text-xs)] text-ink-muted">not judged</td>
      <td colSpan={2} className="px-3 py-2">
        <Link
          to={routePath('retros', { key: projectKey }, { unthemed: 'true' })}
          className="inline-block rounded-control border border-edge bg-panel px-2 py-0.5 text-[length:var(--text-xs)] text-ink no-underline"
        >
          Give them themes
        </Link>
      </td>
    </tr>
  );
}
