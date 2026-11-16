import type { RetroInsightColumnDto, RetroInsightsMode } from '@shiplean/core/api/retros';
import { HeatStrip } from './HeatStrip';

/** The heat strip with its Total and (in sprint mode, when not 0) "n no sprint" that a theme card and the unthemed card share. */
export function StripAndTotal({
  counts,
  columns,
  max,
  label,
  total,
  noSprint,
  mode,
  totalTestId,
  totalDataCell,
  noSprintTestId,
}: {
  counts: Parameters<typeof HeatStrip>[0]['counts'];
  columns: readonly RetroInsightColumnDto[];
  max: number;
  label: string;
  total: number;
  noSprint: number;
  mode: RetroInsightsMode;
  totalTestId: string;
  totalDataCell?: string;
  noSprintTestId?: string;
}) {
  return (
    <div className="grid grid-cols-[minmax(0,1fr)_52px] items-center">
      <HeatStrip counts={counts} columns={columns} max={max} label={label} />
      <div className="grid justify-items-end leading-tight">
        <b
          data-testid={totalTestId}
          data-cell={totalDataCell}
          className="text-[length:var(--text-md)] tabular-nums"
        >
          {total}
        </b>
        {mode === 'sprint' && noSprint > 0 ? (
          <small
            data-testid={noSprintTestId}
            data-count={noSprintTestId ? noSprint : undefined}
            className="text-[length:var(--text-2xs)] whitespace-nowrap text-ink-muted"
          >
            {noSprint} no sprint
          </small>
        ) : null}
      </div>
    </div>
  );
}
