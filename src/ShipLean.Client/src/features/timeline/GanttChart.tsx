import { useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import type { TimelinePersonDto } from '@shiplean/core/api/views';
import { GanttEdgesLayer } from './GanttEdgesLayer';
import type { GanttChartProps } from './GanttChartProps';
import { timelineTestIds as ids } from './testIds';
import {
  AXIS_HEIGHT,
  BRACKET_HEIGHT,
  DIAMOND_SIZE,
  FIT_FALLBACK_WIDTH,
  ROW_HEIGHT,
  rowY,
  timelineGeometry,
  type Mark,
} from './chart/timelineGeometry';
import { timelineRows, type TimelineRow } from './chart/timelineRows';

const LEFT_WIDTH = 240;
const MIN_BAR = 3;

const initials = (person: TimelinePersonDto | null): string =>
  person === null
    ? ''
    : person.name
        .split(/\s+/)
        .filter(Boolean)
        .slice(0, 2)
        .map((word) => word[0].toUpperCase())
        .join('');

/** The scroll area's width, so Fit can divide the range over it. */
function useWidth(): [React.RefObject<HTMLDivElement | null>, number] {
  const ref = useRef<HTMLDivElement | null>(null);
  const [width, setWidth] = useState(FIT_FALLBACK_WIDTH);
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

function rowLabel(row: TimelineRow): { title: string; key: string | null; who: string } {
  switch (row.kind) {
    case 'group':
      return { title: row.label, key: null, who: row.agent ? 'agent' : '' };
    case 'bracket':
      return { title: row.story.title, key: row.key, who: initials(row.story.assignee) };
    case 'bar':
      return { title: row.bar.title, key: row.key, who: initials(row.bar.assignee) };
  }
}

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

export function GanttChart({ view, options, selected, onSelect }: GanttChartProps) {
  const [scrollRef, width] = useWidth();
  const rows = useMemo(() => timelineRows(view, options.group), [view, options.group]);
  const geometry = useMemo(
    () => timelineGeometry(view, rows, options.zoom, width),
    [view, rows, options.zoom, width],
  );
  const leftRef = useRef<HTMLDivElement | null>(null);
  const markRows = useMemo(() => new Map(geometry.marks.map((m) => [m.key, m])), [geometry.marks]);
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

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onSelect(null);
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [onSelect]);

  useEffect(() => {
    if (selected === null) return;
    leftRef.current
      ?.querySelector(`[data-key="${CSS.escape(selected)}"]`)
      ?.scrollIntoView?.({ block: 'nearest' });
  }, [selected]);

  if (view.bars.length === 0 && view.stories.every((s) => s.startHours === null)) {
    return (
      <div
        data-testid={ids.chart}
        data-selected={selected ?? undefined}
        className="p-4 text-ink-muted"
      >
        Nothing has started yet.
      </div>
    );
  }

  const select = (key: string) => (event: React.SyntheticEvent) => {
    event.stopPropagation();
    onSelect(key);
  };
  const halo = selected === null ? undefined : markRows.get(selected);

  return (
    <div
      data-testid={ids.chart}
      data-selected={selected ?? undefined}
      data-group={options.group}
      data-zoom={options.zoom}
      data-deps={options.deps}
      data-cp={options.cp}
      data-view={options.view}
      className="flex rounded-md border border-edge bg-panel"
    >
      <div
        ref={leftRef}
        data-testid={ids.chartLeft}
        className="sticky left-0 z-10 shrink-0 border-r border-edge bg-panel text-[length:var(--text-sm)]"
        style={{ width: LEFT_WIDTH, paddingTop: AXIS_HEIGHT }}
      >
        {rows.map((row) => {
          const label = rowLabel(row);
          return (
            <div
              key={row.key}
              data-testid={ids.chartRow}
              data-key={row.kind === 'group' ? undefined : row.key}
              data-kind={row.kind}
              data-selected={row.key === selected ? true : undefined}
              className="flex items-center gap-2 overflow-hidden px-2 whitespace-nowrap"
              style={{
                height: ROW_HEIGHT,
                fontWeight: row.kind === 'bar' ? 400 : 600,
                background: row.key === selected ? 'var(--color-surface-2)' : undefined,
              }}
            >
              {label.key !== null && <span className="text-ink-muted">{label.key}</span>}
              <span className="truncate">{label.title}</span>
              {label.who !== '' && <span className="ml-auto text-ink-muted">{label.who}</span>}
            </div>
          );
        })}
      </div>
      <div ref={scrollRef} data-testid={ids.chartScroll} className="min-w-0 flex-1 overflow-x-auto">
        <svg
          role="img"
          aria-label={`Timeline of ${view.feature.key}: ${view.bars.length} tasks across ${view.stories.length} stories`}
          width={geometry.width}
          height={geometry.height}
          style={{ display: 'block' }}
        >
          <rect
            x={0}
            y={0}
            width={geometry.width}
            height={geometry.height}
            style={{ fill: 'transparent' }}
            onClick={() => onSelect(null)}
          />
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
                {mark.reviews.map((dot) => (
                  <circle
                    key={`${dot.x}-${dot.verdict}`}
                    data-testid={ids.chartReviewDot}
                    data-key={mark.key}
                    data-kind={dot.verdict}
                    cx={dot.x}
                    cy={mark.box.y + mark.box.height / 2}
                    r={4}
                    style={{
                      fill: dot.verdict === 'approved' ? 'var(--color-ok)' : 'var(--color-warn)',
                    }}
                  >
                    <title>{`${dot.verdict === 'approved' ? 'Approved' : 'Changes requested'}, ${dot.atLabel}`}</title>
                  </circle>
                ))}
              </g>
            );
          })}
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
          <GanttEdgesLayer
            boxes={geometry.boxes}
            edges={view.edges}
            criticalPath={view.criticalPath}
            options={options}
            finished={view.finished}
            pxPerHour={geometry.pxPerHour}
            originX={0}
          />
        </svg>
      </div>
    </div>
  );
}
