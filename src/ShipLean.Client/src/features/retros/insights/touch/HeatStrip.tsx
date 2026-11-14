import type { RetroInsightColumnDto } from '@shiplean/core/api/retros';
import { retrosTestIds as ids } from '../../testIds';
import { heatShade } from '../recurring/heatShade';

/** One cell per shown sprint or week, shaded against the busiest cell of the table, as the desktop's mention cells are. */
export function HeatStrip({
  counts,
  columns,
  max,
  label,
}: {
  counts: readonly number[];
  columns: readonly RetroInsightColumnDto[];
  max: number;
  label: string;
}) {
  return (
    <div
      data-testid={ids.retroHeatStrip}
      role="group"
      aria-label={`${label}: mentions per ${columns[0]?.kind ?? 'sprint'}`}
      className="grid gap-0.5"
      style={{ gridTemplateColumns: `repeat(${columns.length}, minmax(0, 1fr))` }}
    >
      {columns.map((column, i) => {
        const count = counts[i] ?? 0;
        return (
          <span
            key={column.key}
            data-testid={ids.retroHeatCell}
            data-count={count}
            data-column-key={column.key}
            title={`${column.label}: ${count} mention${count === 1 ? '' : 's'}`}
            className={`grid h-[26px] place-items-center rounded-[3px] bg-edge/35 text-[length:var(--text-xs)] tabular-nums ${count === 0 ? 'text-ink-muted' : ''}`}
            style={count > 0 ? { background: heatShade(count, max) } : undefined}
          >
            {count}
          </span>
        );
      })}
    </div>
  );
}
