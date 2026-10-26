import type { VelocityDto } from '../../../api/sprints';
import { sprintsTestIds as ids } from '../testIds';
import { velocityScale } from './velocityScale';

const WIDTH = 760;
const HEIGHT = 180;
const LEFT = 30;
const TOP = 16;
const BOTTOM = 22;
const BAR = 30;

/** The velocity line's label: the project's override when set, else the average of the last sprints. */
function averageLabel(velocity: VelocityDto): string {
  return velocity.override === null
    ? `velocity ${velocity.average} (last ${velocity.sprints.length})`
    : `velocity ${velocity.override} (override)`;
}

/** Grouped committed/completed bars per ended sprint on one y scale, with the velocity line across. Inline SVG, no chart library. */
export function VelocityChart({ velocity }: { velocity: VelocityDto }) {
  const line = velocity.override ?? velocity.average;
  const { max, ticks } = velocityScale([
    line,
    ...velocity.sprints.flatMap((s) => [s.committed, s.completed]),
  ]);
  const y = (value: number) => TOP + (1 - value / max) * (HEIGHT - TOP - BOTTOM);
  const group = (WIDTH - LEFT) / Math.max(1, velocity.sprints.length);
  const names = velocity.sprints.map((s) => s.sprintName).join(', ');

  return (
    <svg
      data-testid={ids.velocityChart}
      viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
      width="100%"
      role="img"
      aria-label={`Committed and completed story points for ${names}; ${averageLabel(velocity)}`}
      className="block max-h-[220px] text-[length:var(--text-xs)]"
      fill="var(--color-muted)"
    >
      {ticks.map((tick) => (
        <g key={tick}>
          <line
            x1={LEFT}
            x2={WIDTH}
            y1={y(tick)}
            y2={y(tick)}
            stroke="var(--color-border)"
            strokeWidth={tick === 0 ? 1 : 0.6}
          />
          <text x={LEFT - 6} y={y(tick) + 3} textAnchor="end">
            {tick}
          </text>
        </g>
      ))}
      <line
        data-testid={ids.velocityAverage}
        data-value={line}
        x1={LEFT}
        x2={WIDTH}
        y1={y(line)}
        y2={y(line)}
        stroke="var(--color-muted)"
        strokeDasharray="4 3"
      />
      <text x={LEFT + 4} y={y(line) - 4}>
        {averageLabel(velocity)}
      </text>
      {velocity.sprints.map((sprint, index) => {
        const centre = LEFT + group * index + group / 2;
        const bars = [
          { kind: 'committed', value: sprint.committed, x: centre - BAR - 1 },
          { kind: 'completed', value: sprint.completed, x: centre + 1 },
        ] as const;
        return (
          <g key={sprint.sprintId}>
            {bars.map((bar) => (
              <rect
                key={bar.kind}
                data-testid={ids.velocityBar}
                data-sprint={sprint.sprintName}
                data-kind={bar.kind}
                data-value={bar.value}
                x={bar.x}
                y={y(bar.value)}
                width={BAR}
                height={y(0) - y(bar.value)}
                rx={3}
                fill={`var(--chart-${bar.kind})`}
              >
                <title>{`${sprint.sprintName}: ${bar.value} ${bar.kind}`}</title>
              </rect>
            ))}
            <text x={centre} y={HEIGHT - 6} textAnchor="middle">
              {sprint.sprintName}
            </text>
            {index === velocity.sprints.length - 1
              ? bars.map((bar) => (
                  <text
                    key={bar.kind}
                    data-testid={ids.velocityLatest}
                    data-latest={bar.kind}
                    x={bar.x + BAR / 2}
                    y={y(bar.value) - 4}
                    textAnchor="middle"
                  >
                    {bar.value}
                  </text>
                ))
              : null}
          </g>
        );
      })}
    </svg>
  );
}
