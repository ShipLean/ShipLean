import { retrosTestIds as ids } from '../../testIds';
import { IMPACT_HEIGHT, IMPACT_WIDTH, impactShape, shortColumnLabel } from './impactGeometry';

/** Mentions per column (sprint or week) as bars, the before and after averages as lines and a dashed marker where the action closed. Inline SVG. */
export function ImpactChart({
  label,
  actionKey,
  columnLabels,
  counts,
  closedAt,
  beforeAvg,
  afterAvg,
  unit,
  selected = null,
  onSelect,
}: {
  label: string;
  actionKey: string;
  columnLabels: readonly string[];
  counts: readonly number[];
  closedAt: number;
  beforeAvg: number;
  afterAvg: number | null;
  unit: 'sprint' | 'week';
  /** The tapped column, outlined. Only the touch layout passes `onSelect`; the hover titles stay either way. */
  selected?: number | null;
  onSelect?: (index: number) => void;
}) {
  const shape = impactShape(counts, closedAt, beforeAvg, afterAvg);
  const markerOnRight = shape.markerX > IMPACT_WIDTH - 90;
  const markerText = closedAt < 0 ? `${actionKey} done earlier` : `${actionKey} done`;
  return (
    <svg
      viewBox={`0 0 ${IMPACT_WIDTH} ${IMPACT_HEIGHT}`}
      width="100%"
      role="img"
      aria-label={`${label} mentions per ${unit}, ${markerText}`}
      className="block text-[length:var(--text-2xs)]"
      data-chart=""
    >
      {shape.ticks.map((tick) => (
        <g key={tick.value}>
          <line
            x1={18}
            x2={IMPACT_WIDTH}
            y1={tick.y}
            y2={tick.y}
            stroke="var(--color-border)"
            strokeWidth={1}
          />
          <text x={2} y={tick.y + 3} fill="var(--color-muted)">
            {tick.value}
          </text>
        </g>
      ))}
      {shape.bars.map((bar) => (
        <g key={bar.index}>
          <rect
            data-bar=""
            data-count={bar.count}
            data-after={bar.after}
            x={bar.x}
            y={bar.y}
            width={bar.width}
            height={bar.height}
            rx={2}
            fill={bar.after ? 'var(--color-ok)' : 'var(--color-warn)'}
            opacity={bar.count > 0 ? 1 : 0.35}
            data-selected={selected === bar.index ? '' : undefined}
            stroke={selected === bar.index ? 'var(--color-text)' : undefined}
            strokeWidth={selected === bar.index ? 2 : undefined}
          >
            <title>{`${columnLabels[bar.index] ?? ''}: ${bar.count}`}</title>
          </rect>
          <text x={bar.labelX} y={IMPACT_HEIGHT - 4} textAnchor="middle" fill="var(--color-muted)">
            {shortColumnLabel(columnLabels[bar.index] ?? '')}
          </text>
          {onSelect ? (
            <rect
              data-testid={ids.retroImpactBar}
              data-index={bar.index}
              data-selected={selected === bar.index ? '' : undefined}
              x={bar.x - 5}
              y={0}
              width={bar.width + 10}
              height={IMPACT_HEIGHT}
              fill="transparent"
              onClick={() => onSelect(bar.index)}
            />
          ) : null}
        </g>
      ))}
      {shape.before ? (
        <line
          data-line="before"
          data-value={shape.before.value}
          x1={shape.before.x1}
          x2={shape.before.x2}
          y1={shape.before.y}
          y2={shape.before.y}
          stroke="var(--color-text)"
          strokeWidth={1.5}
        />
      ) : null}
      {shape.after ? (
        <line
          data-line="after"
          data-value={shape.after.value}
          x1={shape.after.x1}
          x2={shape.after.x2}
          y1={shape.after.y}
          y2={shape.after.y}
          stroke="var(--color-text)"
          strokeWidth={1.5}
        />
      ) : null}
      <line
        data-marker=""
        x1={shape.markerX}
        x2={shape.markerX}
        y1={4}
        y2={shape.baselineY}
        stroke="var(--color-accent)"
        strokeWidth={1.5}
        strokeDasharray="3 3"
      />
      <text
        x={markerOnRight ? shape.markerX - 4 : shape.markerX + 4}
        y={12}
        textAnchor={markerOnRight ? 'end' : 'start'}
        fill="var(--color-accent)"
        fontWeight={600}
      >
        {markerText}
      </text>
    </svg>
  );
}
