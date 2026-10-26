import type { SprintMetricsDto } from '../../../api/sprints';
import { sprintsTestIds as ids } from '../testIds';
import { BURNDOWN_HEIGHT, BURNDOWN_WIDTH, burndownShape } from './burndownGeometry';

/** Story points remaining at the end of each working day so far, over the whole sprint's working days, against the ideal line. Inline SVG, no chart library. */
export function Burndown({
  metrics,
  days,
}: {
  metrics: SprintMetricsDto;
  /** Every working day of the sprint (yyyy-MM-dd), the chart's x-axis. */
  days: readonly string[];
}) {
  const shape = burndownShape(metrics.burndown, days, metrics.committed);
  return (
    <div className="rounded-md border border-edge bg-panel px-2.5 py-1.5">
      <header className="flex justify-between gap-2 text-[length:var(--text-sm)] font-semibold">
        <span>Burndown</span>
        <span data-testid={ids.burndownRemaining} className="font-medium text-ink-muted">
          {metrics.remaining} pts left
        </span>
      </header>
      {metrics.burndown.length === 0 ? (
        <p
          data-testid={ids.burndownEmpty}
          className="m-0 text-[length:var(--text-sm)] text-ink-muted"
        >
          Starts with the first working day.
        </p>
      ) : (
        <svg
          data-testid={ids.burndown}
          viewBox={`0 0 ${BURNDOWN_WIDTH} ${BURNDOWN_HEIGHT}`}
          width={BURNDOWN_WIDTH}
          height={BURNDOWN_HEIGHT}
          role="img"
          aria-label="Burndown: story points remaining per working day against the ideal line"
          className="block text-[length:9px]"
          fill="var(--color-muted)"
        >
          <line
            x1={24}
            x2={BURNDOWN_WIDTH - 6}
            y1={shape.baselineY}
            y2={shape.baselineY}
            stroke="var(--color-border)"
          />
          <text x={2} y={9}>
            {shape.max}
          </text>
          <polyline
            data-testid={ids.burndownIdeal}
            points={shape.idealPoints}
            fill="none"
            stroke="var(--color-muted)"
            strokeWidth={1.5}
            strokeDasharray="3 3"
          />
          <polyline
            data-testid={ids.burndownActual}
            points={shape.actualPoints}
            fill="none"
            stroke="var(--chart-completed)"
            strokeWidth={2}
            strokeLinejoin="round"
          />
          {shape.dots.map((dot) => (
            <g key={dot.date}>
              <circle
                data-testid={ids.burndownPoint}
                data-day={dot.day}
                data-remaining={dot.remaining}
                data-ideal={dot.ideal}
                cx={dot.x}
                cy={dot.y}
                r={3}
                fill="var(--chart-completed)"
                stroke="var(--color-surface)"
                strokeWidth={2}
              >
                <title>{`Day ${dot.day}: ${dot.remaining} pts left (ideal ${dot.ideal})`}</title>
              </circle>
              <text x={dot.x} y={BURNDOWN_HEIGHT - 4} textAnchor="middle">
                {dot.day}
              </text>
            </g>
          ))}
        </svg>
      )}
    </div>
  );
}
