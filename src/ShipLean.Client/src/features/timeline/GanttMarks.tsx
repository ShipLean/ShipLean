import { useMemo } from 'react';
import type { TimelineStoryDto } from '@shiplean/core/api/views';
import type { GanttMarksProps } from './GanttMarksProps';
import { timelineTestIds as ids } from './testIds';
import { DIAMOND_SIZE, MIN_BAR, REVIEW_DOT_RADIUS, type Mark } from './chart/timelineGeometry';
import { DURATION_LABEL_FONT_SIZE, durationLabelBox } from './chart/labelBoxes';

/** Off-path marks fade to this while the critical path is shown (SHIP-1612 §3). */
const FADED_OPACITY = 0.35;
/** How far the open bar's end cue reaches past the bar's end (now). */
const OPEN_END_CUE = 5;

function MarkShape({
  mark,
  title,
  cp,
  story,
  open,
}: {
  mark: Mark;
  title: string;
  /** Outlined: the mark is on the critical path and the path is shown. */
  cp: boolean;
  /** The story or bug a bracket stands for; undefined for a bar or diamond. */
  story: TimelineStoryDto | undefined;
  /** The bar is still open (in flight): it ends at now with an end cue. */
  open: boolean;
}) {
  const { box } = mark;
  const cy = box.y + box.height / 2;
  if (mark.kind === 'diamond') {
    const d = DIAMOND_SIZE;
    return (
      <>
        <polygon
          points={`${box.x0},${cy - d} ${box.x0 + d},${cy} ${box.x0},${cy + d} ${box.x0 - d},${cy}`}
          style={{
            fill: 'var(--color-surface)',
            stroke: cp ? 'var(--color-text)' : 'var(--color-muted)',
            strokeWidth: 1.5,
            strokeDasharray: '2 2',
          }}
        >
          <title>{title}</title>
        </polygon>
        <text
          data-testid={ids.chartInferredNote}
          x={box.x0 + d + 5}
          y={cy}
          dominantBaseline="central"
          style={{ fontSize: DURATION_LABEL_FONT_SIZE, fill: 'var(--color-muted)' }}
        >
          Closed, no start recorded
        </text>
      </>
    );
  }
  if (mark.kind === 'bracket') {
    const { x0, y, height } = box;
    const x1 = Math.max(box.x1, x0 + MIN_BAR);
    const bug = story?.type === 'bug';
    // A bracket over its tasks: the top line with end ticks reaching down; dashed while the story is still open.
    return (
      <path
        d={`M${x0},${y + height} V${y} H${x1} V${y + height}`}
        style={{
          fill: 'none',
          stroke: bug ? 'var(--tl-badge-bug)' : 'var(--tl-badge-story)',
          strokeWidth: 2.5,
          strokeDasharray: story?.endHours === null ? '5 3' : undefined,
        }}
      >
        <title>{title}</title>
      </path>
    );
  }
  const x1 = box.x0 + Math.max(box.x1 - box.x0, MIN_BAR);
  return (
    <>
      <rect
        x={box.x0}
        y={box.y}
        width={x1 - box.x0}
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
      {open && (
        <path
          data-testid={ids.chartOpenEnd}
          d={`M${x1},${box.y - 2} L${x1 + OPEN_END_CUE},${cy} L${x1},${box.y + box.height + 2} Z`}
          style={{ fill: 'var(--chart-completed)' }}
        />
      )}
    </>
  );
}

/** The selection halo, then each mark with its review line, commit tick, review dots and duration label. */
export function GanttMarks({ view, geometry, options, selected, onSelect }: GanttMarksProps) {
  const markRows = useMemo(() => new Map(geometry.marks.map((m) => [m.key, m])), [geometry.marks]);
  const barsByKey = useMemo(() => new Map(view.bars.map((b) => [b.key, b])), [view.bars]);
  const storiesByKey = useMemo(() => new Map(view.stories.map((s) => [s.key, s])), [view.stories]);
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

  const showCp = options.cp && view.finished;
  const onPath = useMemo(() => new Set(view.criticalPath), [view.criticalPath]);
  // A bar is on the path when it is in criticalPath; a bracket when any of its tasks is (D10).
  const isOnPath = (key: string): boolean => {
    const story = storiesByKey.get(key);
    return story === undefined ? onPath.has(key) : story.tasks.some((task) => onPath.has(task));
  };

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
        const markOnPath = showCp && isOnPath(mark.key);
        const faded = showCp && !markOnPath;
        const bar = barsByKey.get(mark.key);
        const cy = mark.box.y + mark.box.height / 2;
        // The server sends the hours and the label together, both null when no commit time is known.
        const commit =
          bar !== undefined && bar.lastCommitHours !== null && bar.lastCommitLabel !== null
            ? { x: bar.lastCommitHours * geometry.pxPerHour, label: bar.lastCommitLabel }
            : null;
        const label =
          mark.durationLabel === null ? null : durationLabelBox(mark, mark.durationLabel);
        return (
          <g
            key={mark.key}
            data-faded={faded}
            style={faded ? { opacity: FADED_OPACITY } : undefined}
          >
            {mark.reviewLine && (
              <line
                x1={mark.reviewLine.x0}
                x2={mark.reviewLine.x1}
                y1={cy}
                y2={cy}
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
              <MarkShape
                mark={mark}
                title={title}
                cp={markOnPath && mark.kind !== 'bracket'}
                story={storiesByKey.get(mark.key)}
                open={bar?.state === 'inFlight'}
              />
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
                cy={cy}
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
            {label !== null && (
              <text
                data-testid={ids.chartDurationLabel}
                data-key={mark.key}
                x={label.x0}
                y={cy}
                dominantBaseline="central"
                style={{
                  fontSize: DURATION_LABEL_FONT_SIZE,
                  fill: 'var(--color-muted)',
                  pointerEvents: 'none',
                }}
              >
                {mark.durationLabel}
              </text>
            )}
          </g>
        );
      })}
    </>
  );
}
