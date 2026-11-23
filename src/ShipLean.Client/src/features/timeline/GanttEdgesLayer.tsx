import { edgePairKey, edgePaths } from './edges/edgePaths';
import { placeWaitLabels, WAIT_LABEL_FONT_SIZE, WAIT_LABEL_HALO } from './edges/waitLabels';
import { timelineTestIds as ids } from './testIds';
import type { GanttEdgesLayerProps } from './GanttEdgesLayerProps';

/** With Critical path on, arrows off the path fade like the bars. */
const FADED = 0.35;

/** Dependency arrows, wait labels and the critical path outline, drawn inside the chart's SVG. */
export function GanttEdgesLayer({
  boxes,
  marks,
  edges,
  criticalPath,
  options,
  finished,
  pxPerHour,
  originX,
}: GanttEdgesLayerProps) {
  const paths = options.deps ? edgePaths(marks, edges, { pxPerHour, originX }) : [];
  const labels = placeWaitLabels(edges, paths, marks);
  const outline = options.deps && options.cp && finished;
  const onPath = new Set(criticalPath);
  const fadedPairs = new Set(
    paths
      .filter((p) => outline && !p.onCriticalPath && p.kind !== 'blocked')
      .map((p) => edgePairKey(p)),
  );
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
            key={edgePairKey(p)}
            data-testid={ids.edge}
            data-from={p.from}
            data-to={p.to}
            data-kind={p.kind}
            data-critical={critical}
            opacity={fadedPairs.has(edgePairKey(p)) ? FADED : undefined}
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
          </g>
        );
      })}
      {/* Labels after every arrow, so each halo clears the lines under it. */}
      {labels.map((l) => (
        <text
          key={edgePairKey(l)}
          data-testid={ids.edgeWaitLabel}
          data-from={l.from}
          data-to={l.to}
          x={l.x}
          y={l.y}
          textAnchor="middle"
          dominantBaseline="central"
          fontSize={WAIT_LABEL_FONT_SIZE}
          fontWeight={600}
          fill="var(--color-text)"
          stroke="var(--color-panel)"
          strokeWidth={WAIT_LABEL_HALO}
          strokeLinejoin="round"
          paintOrder="stroke"
          opacity={fadedPairs.has(edgePairKey(l)) ? FADED : undefined}
          pointerEvents="none"
        >
          {l.text}
        </text>
      ))}
    </g>
  );
}
