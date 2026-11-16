import { edgePaths } from './edges/edgePaths';
import { timelineTestIds as ids } from './testIds';
import type { GanttEdgesLayerProps } from './GanttEdgesLayerProps';

/** Dependency arrows, wait labels and the critical path outline, drawn inside the chart's SVG. */
export function GanttEdgesLayer({
  boxes,
  edges,
  criticalPath,
  options,
  finished,
  pxPerHour,
  originX,
}: GanttEdgesLayerProps) {
  const paths = options.deps ? edgePaths(boxes, edges, { pxPerHour, originX }) : [];
  const outline = options.deps && options.cp && finished;
  const onPath = new Set(criticalPath);
  return (
    <g data-testid={ids.edges} data-boxes={boxes.length}>
      {outline &&
        boxes
          .filter((b) => onPath.has(b.key))
          .map((b) => (
            <rect
              key={`cp-${b.key}`}
              data-testid={ids.edgeCritical}
              data-key={b.key}
              x={b.x0 - 2}
              y={b.y - 2}
              width={b.x1 - b.x0 + 4}
              height={b.height + 4}
              rx={4}
              fill="none"
              stroke="var(--color-text)"
              strokeWidth={1.5}
              pointerEvents="none"
            />
          ))}
      {paths.map((p) => {
        const critical = outline && p.onCriticalPath;
        const blocked = p.kind === 'blocked';
        const colour = blocked ? 'var(--color-danger)' : 'var(--color-text)';
        return (
          <g
            key={`${p.kind}-${p.from}-${p.to}`}
            data-testid={ids.edge}
            data-from={p.from}
            data-to={p.to}
            data-kind={p.kind}
            data-critical={critical}
            pointerEvents="none"
          >
            <title>{p.title}</title>
            <path
              d={p.d}
              fill="none"
              stroke={colour}
              strokeWidth={critical ? 2 : 1}
              strokeOpacity={critical || blocked ? 1 : 0.55}
              strokeDasharray={blocked ? '4 3' : undefined}
            />
            <path d={p.head} fill={colour} fillOpacity={critical || blocked ? 1 : 0.55} />
            {p.waitLabel !== null && (
              <text
                data-testid={ids.edgeWaitLabel}
                x={p.labelX}
                y={p.labelY}
                fontSize={10}
                fill="var(--color-text)"
              >
                {p.waitLabel}
              </text>
            )}
          </g>
        );
      })}
    </g>
  );
}
