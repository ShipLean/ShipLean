import { useLayoutEffect, useMemo, useRef, useState } from 'react';
import { inspectionMetricsTestIds as ids } from '../testIds';
import type { LeadTimeChartProps } from './LeadTimeChartProps';
import { CHART_HEIGHT, FALLBACK_WIDTH, leadTimeLayout, type ChartDot } from './chartGeometry';

const TEXT = { fontSize: 10.5, fill: 'var(--color-muted)', fontFamily: 'var(--font-sans)' };
const VALUE = { ...TEXT, fill: 'var(--color-text)', fontWeight: 600 };
/** A halo so a label stays readable over bars and dots. */
const HALO = {
  paintOrder: 'stroke',
  stroke: 'var(--color-surface)',
  strokeWidth: 4,
  strokeLinejoin: 'round',
} as const;

/** The chart's own width, so the columns divide what is really there. */
function useWidth(): [React.RefObject<HTMLDivElement | null>, number] {
  const ref = useRef<HTMLDivElement | null>(null);
  const [width, setWidth] = useState(FALLBACK_WIDTH);
  useLayoutEffect(() => {
    const el = ref.current;
    if (el === null) return;
    const measure = () => {
      if (el.clientWidth > 0) setWidth(el.clientWidth);
    };
    measure();
    const observer = new ResizeObserver(measure);
    observer.observe(el);
    return () => observer.disconnect();
  }, []);
  return [ref, width];
}

const onActivate = (act: () => void) => (event: React.KeyboardEvent) => {
  if (event.key !== 'Enter' && event.key !== ' ') return;
  event.preventDefault();
  event.stopPropagation();
  act();
};

const dotTitle = (dot: ChartDot) =>
  `${dot.taskKey} ${dot.title} · ${dot.leadLabel}${dot.endedBy === 'resolved' ? ' (to Resolved)' : ''}`;

export function LeadTimeChart({
  periods,
  height = CHART_HEIGHT,
  onPeriod,
  onDot,
}: LeadTimeChartProps) {
  const [ref, available] = useWidth();
  const layout = useMemo(
    () => leadTimeLayout(periods, available, height),
    [periods, available, height],
  );

  // One outer div in both branches: useWidth observes it once, so it must survive a change between empty and periods.
  if (layout.empty) {
    return (
      <div ref={ref} className="min-w-0">
        <div
          data-testid={ids.chartEmpty}
          className="grid place-items-center text-[length:var(--text-sm)] text-ink-muted"
          style={{ height }}
        >
          No sprints in this range yet.
        </div>
      </div>
    );
  }

  const { width, top, left } = layout;
  return (
    <div ref={ref} className="min-w-0">
      <div data-testid={ids.chart}>
        <svg
          role="img"
          aria-label={`Mean lead time per sprint with one dot per task, over ${periods.length} sprints`}
          viewBox={`0 0 ${width} ${height}`}
          width="100%"
          height={height}
          style={{ display: 'block' }}
        >
          {layout.ticks.map((tick) => (
            <g key={tick.label}>
              <line
                x1={left}
                x2={width}
                y1={tick.y}
                y2={tick.y}
                style={{ stroke: 'var(--color-border)', strokeWidth: tick.base ? 1 : 0.6 }}
              />
              <text x={left - 6} y={tick.y + 3} textAnchor="end" style={TEXT}>
                {tick.label}
              </text>
            </g>
          ))}
          {layout.quarters.map((quarter) => (
            <g key={quarter.label}>
              {quarter.divider && (
                <line
                  x1={quarter.x - 4}
                  x2={quarter.x - 4}
                  y1={top - 6}
                  y2={height - 4}
                  style={{ stroke: 'var(--color-muted)', strokeDasharray: '2 3' }}
                />
              )}
              <text x={quarter.x} y={top - 2} style={TEXT}>
                {quarter.label}
              </text>
            </g>
          ))}
          {layout.bars.map((bar) => (
            <g
              key={bar.periodId}
              data-testid={ids.chartBar}
              data-period-id={bar.periodId}
              data-in-progress={bar.inProgress || undefined}
              role="button"
              tabIndex={0}
              aria-label={`${bar.name}: mean ${bar.meanLabel ?? 'none'} over ${bar.tasks} tasks. Open the sprint.`}
              style={{ cursor: 'pointer' }}
              onClick={() => onPeriod(bar.periodId)}
              onKeyDown={onActivate(() => onPeriod(bar.periodId))}
            >
              <title>{`${bar.name}: mean ${bar.meanLabel ?? 'none'} over ${bar.tasks} tasks. Click to open.`}</title>
              <rect
                x={bar.hitX}
                y={top}
                width={bar.hitWidth}
                height={height - top - 4}
                style={{ fill: 'transparent' }}
              />
              {bar.top !== null && (
                <>
                  <rect
                    x={bar.x}
                    y={bar.top}
                    width={bar.width}
                    height={bar.bottom - bar.top}
                    rx={3}
                    style={{
                      fill: bar.inProgress
                        ? 'none'
                        : 'color-mix(in srgb, var(--chart-completed) 30%, transparent)',
                      stroke: 'var(--chart-completed)',
                      strokeWidth: 1.2,
                      strokeDasharray: bar.inProgress ? '4 3' : undefined,
                    }}
                  />
                  <line
                    x1={bar.x - 3}
                    x2={bar.x + bar.width + 3}
                    y1={bar.top}
                    y2={bar.top}
                    style={{ stroke: 'var(--chart-completed)', strokeWidth: 2.4 }}
                  />
                </>
              )}
              <text
                x={bar.cx}
                y={layout.periodLabelY}
                textAnchor="middle"
                style={{ ...VALUE, fontWeight: 500 }}
              >
                {bar.shortName}
              </text>
              <text x={bar.cx} y={layout.tasksLabelY} textAnchor="middle" style={TEXT}>
                {`${bar.tasks} task${bar.tasks === 1 ? '' : 's'}`}
                {bar.inProgress && <tspan data-testid={ids.chartSoFar}> · so far</tspan>}
              </text>
            </g>
          ))}
          {layout.dots.map((dot) => (
            <circle
              key={dot.taskKey}
              data-testid={ids.chartDot}
              data-key={dot.taskKey}
              data-over={dot.over || undefined}
              role="button"
              tabIndex={0}
              aria-label={dotTitle(dot)}
              cx={dot.cx}
              cy={dot.cy}
              r={dot.r}
              style={{
                cursor: 'pointer',
                fill: dot.over ? 'var(--color-danger)' : 'var(--color-text)',
                fillOpacity: dot.over ? 0.9 : 0.38,
              }}
              onClick={(event) => {
                event.stopPropagation();
                onDot(dot.taskKey);
              }}
              onKeyDown={onActivate(() => onDot(dot.taskKey))}
            >
              <title>{dotTitle(dot)}</title>
            </circle>
          ))}
          {layout.bars.map(
            (bar) =>
              bar.top !== null && (
                <text
                  key={bar.periodId}
                  x={bar.cx}
                  y={bar.top - 6}
                  textAnchor="middle"
                  pointerEvents="none"
                  style={{ ...VALUE, ...HALO }}
                >
                  {bar.meanLabel}
                </text>
              ),
          )}
          {layout.dots
            .filter((dot) => dot.longest)
            .map((dot) => (
              <text
                key={dot.taskKey}
                data-testid={ids.chartDotLabel}
                data-key={dot.taskKey}
                x={dot.cx + 6}
                y={dot.over ? top + 4 : dot.cy + 3}
                pointerEvents="none"
                style={{
                  ...TEXT,
                  ...HALO,
                  fill: dot.over ? 'var(--color-danger)' : 'var(--color-text)',
                  fontWeight: 600,
                }}
              >
                {`${dot.taskKey} ${dot.leadLabel}`}
              </text>
            ))}
        </svg>
      </div>
    </div>
  );
}
