import { useMemo } from 'react';
import type { GanttMarksProps } from './GanttMarksProps';
import { timelineTestIds as ids } from './testIds';
import {
  BRACKET_HEIGHT,
  DIAMOND_SIZE,
  MIN_BAR,
  REVIEW_DOT_RADIUS,
  type Mark,
} from './chart/timelineGeometry';

function MarkShape({ mark, title, cp }: { mark: Mark; title: string; cp: boolean }) {
  const { box } = mark;
  if (mark.kind === 'diamond') {
    const c = box.y + box.height / 2;
    const d = DIAMOND_SIZE;
    return (
      <polygon
        points={`${box.x0},${c - d} ${box.x0 + d},${c} ${box.x0},${c + d} ${box.x0 - d},${c}`}
        style={{
          fill: 'var(--chart-completed)',
          stroke: cp ? 'var(--color-text)' : 'none',
          strokeWidth: 2,
        }}
      >
        <title>{title}</title>
      </polygon>
    );
  }
  if (mark.kind === 'bracket') {
    const { x0, y } = box;
    const x1 = Math.max(box.x1, x0 + MIN_BAR);
    return (
      <>
        <rect
          x={x0}
          y={y}
          width={x1 - x0}
          height={BRACKET_HEIGHT}
          style={{ fill: 'var(--color-text)', opacity: 0.12 }}
        />
        <path
          d={`M${x0},${y} V${y + BRACKET_HEIGHT} H${x1} V${y}`}
          style={{ fill: 'none', stroke: 'var(--color-text)', strokeWidth: 2 }}
        >
          <title>{title}</title>
        </path>
      </>
    );
  }
  return (
    <rect
      x={box.x0}
      y={box.y}
      width={Math.max(box.x1 - box.x0, MIN_BAR)}
      height={box.height}
      rx={3}
      style={{
        fill: 'var(--chart-completed)',
        stroke: cp ? 'var(--color-text)' : 'none',
        strokeWidth: 2,
      }}
    >
      <title>{title}</title>
    </rect>
  );
}

/** The selection halo, then each mark with its review line, commit tick and review dots. */
export function GanttMarks({ view, geometry, options, selected, onSelect }: GanttMarksProps) {
  const markRows = useMemo(() => new Map(geometry.marks.map((m) => [m.key, m])), [geometry.marks]);
  const barsByKey = useMemo(() => new Map(view.bars.map((b) => [b.key, b])), [view.bars]);
  const titles = useMemo(() => {
    const map = new Map<string, string>();
    for (const bar of view.bars)
      map.set(
        bar.key,
        `${bar.key} ${bar.title}${bar.durationLabel ? `, ${bar.durationLabel}` : ''}`,
      );
    for (const story of view.stories)
      map.set(
        story.key,
        `${story.key} ${story.title}${story.durationLabel ? `, ${story.durationLabel}` : ''}`,
      );
    return map;
  }, [view]);

  const select = (key: string) => (event: React.SyntheticEvent) => {
    event.stopPropagation();
    onSelect(key);
  };
  const halo = selected === null ? undefined : markRows.get(selected);

  return (
    <>
      {halo && (
        <rect
          x={halo.box.x0 - 4}
          y={halo.box.y - 4}
          width={halo.box.x1 - halo.box.x0 + 8}
          height={halo.box.height + 8}
          rx={5}
          style={{
            fill: 'none',
            stroke: 'var(--chart-completed)',
            strokeWidth: 2,
            opacity: 0.5,
          }}
        />
      )}
      {geometry.marks.map((mark) => {
        const testId =
          mark.kind === 'bar'
            ? ids.chartBar
            : mark.kind === 'bracket'
              ? ids.chartBracket
              : ids.chartDiamond;
        const title = titles.get(mark.key) ?? mark.key;
        const cp = options.cp && view.finished && view.criticalPath.includes(mark.key);
        const bar = barsByKey.get(mark.key);
        // The server sends the hours and the label together, both null when no commit time is known.
        const commit =
          bar !== undefined && bar.lastCommitHours !== null && bar.lastCommitLabel !== null
            ? { x: bar.lastCommitHours * geometry.pxPerHour, label: bar.lastCommitLabel }
            : null;
        return (
          <g key={mark.key}>
            {mark.reviewLine && (
              <line
                x1={mark.reviewLine.x0}
                x2={mark.reviewLine.x1}
                y1={mark.box.y + mark.box.height / 2}
                y2={mark.box.y + mark.box.height / 2}
                style={{ stroke: 'var(--color-muted)', strokeWidth: 1, strokeDasharray: '3 3' }}
              />
            )}
            <g
              data-testid={testId}
              data-key={mark.key}
              data-kind={mark.kind}
              tabIndex={0}
              role="button"
              aria-label={title}
              style={{ cursor: 'pointer' }}
              onClick={select(mark.key)}
              onKeyDown={(event) => {
                if (event.key === 'Enter' || event.key === ' ') {
                  event.preventDefault();
                  select(mark.key)(event);
                }
              }}
            >
              <MarkShape mark={mark} title={title} cp={cp} />
            </g>
            {commit !== null && (
              <line
                data-testid={ids.chartCommitTick}
                data-key={mark.key}
                x1={commit.x}
                x2={commit.x}
                y1={mark.box.y - 3}
                y2={mark.box.y + mark.box.height + 3}
                style={{ stroke: 'var(--color-text)', strokeWidth: 2, pointerEvents: 'none' }}
              >
                <title>{`Last commit ${commit.label}`}</title>
              </line>
            )}
            {mark.reviews.map((dot) => (
              <circle
                key={`${dot.x}-${dot.verdict}`}
                data-testid={ids.chartReviewDot}
                data-key={mark.key}
                data-kind={dot.verdict}
                cx={dot.x}
                cy={mark.box.y + mark.box.height / 2}
                r={REVIEW_DOT_RADIUS}
                style={{
                  fill: dot.verdict === 'approved' ? 'var(--color-ok)' : 'var(--color-warn)',
                  cursor: 'pointer',
                }}
                // A review from an earlier round sits on its bar; clicking it selects the bar it covers.
                onClick={select(mark.key)}
              >
                <title>{`${dot.verdict === 'approved' ? 'Approved' : 'Changes requested'}, ${dot.atLabel}`}</title>
              </circle>
            ))}
          </g>
        );
      })}
    </>
  );
}
