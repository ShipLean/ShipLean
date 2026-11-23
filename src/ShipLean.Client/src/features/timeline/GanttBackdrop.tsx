import type { GanttBackdropProps } from './GanttBackdropProps';
import { timelineTestIds as ids } from './testIds';
import { AXIS_HEIGHT, rowY } from './chart/timelineGeometry';

/** Drawn first, under the marks: sprint band, day labels, weekends, row lines and the now line. */
export function GanttBackdrop({ rows, geometry, options }: GanttBackdropProps) {
  return (
    <>
      {geometry.sprints.map((band, i) => (
        <g key={band.name} data-testid={ids.chartSprintBand}>
          <rect
            x={band.x}
            y={0}
            width={band.width}
            height={14}
            style={{ fill: i % 2 === 0 ? 'var(--color-surface-2)' : 'var(--color-border)' }}
          />
          <text x={band.x + 4} y={11} fontSize={10} style={{ fill: 'var(--color-muted)' }}>
            {band.name}
          </text>
        </g>
      ))}
      {geometry.days.map((day) => (
        <g key={day.x}>
          {day.weekend && (
            <rect
              data-testid={ids.chartWeekend}
              x={day.x}
              y={14}
              width={day.width}
              height={geometry.height - 14}
              style={{ fill: 'var(--color-surface-2)', opacity: 0.6 }}
            />
          )}
          <text
            data-testid={ids.chartDayLabel}
            x={day.x + day.width / 2}
            y={AXIS_HEIGHT - 8}
            fontSize={10}
            textAnchor="middle"
            style={{ fill: 'var(--color-muted)' }}
          >
            {options.zoom === 'days' ? day.label : day.dayOfMonth}
          </text>
        </g>
      ))}
      {rows.map((row, i) => (
        <line
          key={row.key}
          x1={0}
          x2={geometry.width}
          y1={rowY(i + 1)}
          y2={rowY(i + 1)}
          style={{ stroke: 'var(--color-border)', strokeWidth: 0.5 }}
        />
      ))}
      {geometry.nowX !== null && (
        <line
          data-testid={ids.chartNowLine}
          x1={geometry.nowX}
          x2={geometry.nowX}
          y1={14}
          y2={geometry.height}
          style={{ stroke: 'var(--color-danger)', strokeWidth: 1.5, strokeDasharray: '4 3' }}
        />
      )}
    </>
  );
}
