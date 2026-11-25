import type { GanttBackdropProps } from './GanttBackdropProps';
import { timelineTestIds as ids } from './testIds';
import { AXIS_HEIGHT, ROW_HEIGHT, rowY } from './chart/timelineGeometry';

/** Drawn first, under the marks: sprint band, day labels, weekends, row lines and the now line. */
export function GanttBackdrop({ rows, geometry, selected }: GanttBackdropProps) {
  const selectedRow = selected === null ? -1 : rows.findIndex((row) => row.key === selected);
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
        </g>
      ))}
      {geometry.axisLabels.map((label) => (
        <g key={`${label.kind}-${label.x}`}>
          {label.kind === 'week' && (
            <line
              data-testid={ids.chartWeekTick}
              x1={label.x}
              x2={label.x}
              y1={AXIS_HEIGHT - 14}
              y2={geometry.height}
              style={{ stroke: 'var(--color-border)', strokeWidth: 0.5 }}
            />
          )}
          <text
            data-testid={ids.chartDayLabel}
            data-kind={label.kind}
            x={label.kind === 'week' ? label.x + 4 : label.x}
            y={AXIS_HEIGHT - 8}
            fontSize={10}
            textAnchor={label.kind === 'week' ? 'start' : 'middle'}
            style={{ fill: 'var(--color-muted)' }}
          >
            {label.text}
          </text>
        </g>
      ))}
      {selectedRow >= 0 && (
        <rect
          data-testid={ids.chartSelectedBand}
          x={0}
          y={rowY(selectedRow)}
          width={geometry.width}
          height={ROW_HEIGHT}
          style={{ fill: 'var(--tl-selected-band)', pointerEvents: 'none' }}
        />
      )}
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
        <>
          <text
            data-testid={ids.chartNowLabel}
            x={geometry.nowX - 6}
            y={AXIS_HEIGHT - 8}
            fontSize={10}
            fontWeight={600}
            textAnchor="end"
            style={{ fill: 'var(--tl-accent)' }}
          >
            Now
          </text>
          <line
            data-testid={ids.chartNowLine}
            x1={geometry.nowX}
            x2={geometry.nowX}
            y1={14}
            y2={geometry.height}
            style={{ stroke: 'var(--tl-accent)', strokeWidth: 1.5, strokeDasharray: '4 3' }}
          />
        </>
      )}
    </>
  );
}
