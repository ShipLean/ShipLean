import { retroSharedTestIds as ids } from './testIds';

/** Mentions per sprint as small bars, with a dashed line where a fix closed. Inline SVG, no chart library. */
export function MentionStrip({
  counts,
  fixAt,
  label,
  sprintNames,
  width = 96,
  height = 22,
}: {
  /** Mentions per sprint, one bar each. */
  counts: readonly number[];
  /** Index into `counts` of the bar a fix closed after, or null. */
  fixAt: number | null;
  /** What the strip counts, for the accessible name (the theme's name). */
  label: string;
  /** The view's sprint names, aligned with `counts`, for the bar tooltips. */
  sprintNames: readonly string[];
  width?: number;
  height?: number;
}) {
  const max = Math.max(4, ...counts);
  const barWidth = width / counts.length;
  return (
    <svg
      data-testid={ids.retroMentionStrip}
      viewBox={`0 0 ${width} ${height}`}
      width={width}
      height={height}
      role="img"
      aria-label={`${label}: mentions per sprint ${counts.join(', ')}`}
      className="block flex-none"
    >
      {counts.map((n, i) => {
        const barHeight = Math.max(n > 0 ? (n / max) * (height - 4) : 1, 1);
        return (
          <rect
            key={i}
            data-bar=""
            x={i * barWidth + 1.5}
            y={height - barHeight}
            width={barWidth - 3}
            height={barHeight}
            rx={1.5}
            fill={n > 0 ? 'var(--color-warn)' : 'var(--color-border)'}
          >
            <title>{`${sprintNames[i] ?? ''}: ${n} mention${n === 1 ? '' : 's'}`}</title>
          </rect>
        );
      })}
      {fixAt !== null ? (
        <line
          data-fix=""
          data-fix-at={fixAt}
          x1={(fixAt + 1) * barWidth}
          x2={(fixAt + 1) * barWidth}
          y1={0}
          y2={height}
          stroke="var(--color-ok)"
          strokeWidth={1.5}
          strokeDasharray="2 2"
        />
      ) : null}
    </svg>
  );
}
