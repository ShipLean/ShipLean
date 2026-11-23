import { useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { GanttCanvas } from './GanttCanvas';
import type { GanttChartProps } from './GanttChartProps';
import { GanttRowLabels } from './GanttRowLabels';
import { timelineTestIds as ids } from './testIds';
import { FIT_FALLBACK_WIDTH, timelineGeometry } from './chart/timelineGeometry';
import { timelineRows } from './chart/timelineRows';
import './parts/timelineTokens.css';

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

/** The chart's shell: computes the rows and geometry once, owns Escape and scrolling the selected row into view. */
export function GanttChart({ view, options, selected, onSelect }: GanttChartProps) {
  const [scrollRef, width] = useWidth();
  const rows = useMemo(() => timelineRows(view, options.group), [view, options.group]);
  const geometry = useMemo(
    () => timelineGeometry(view, rows, options.zoom, width),
    [view, rows, options.zoom, width],
  );
  const rootRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onSelect(null);
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [onSelect]);

  useEffect(() => {
    if (selected === null) return;
    rootRef.current
      ?.querySelector(`[data-testid="${ids.chartRow}"][data-key="${CSS.escape(selected)}"]`)
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

  return (
    <div
      ref={rootRef}
      data-testid={ids.chart}
      data-selected={selected ?? undefined}
      data-group={options.group}
      data-zoom={options.zoom}
      data-deps={options.deps}
      data-cp={options.cp}
      data-view={options.view}
      className="flex rounded-md border border-edge bg-panel"
    >
      <GanttRowLabels
        view={view}
        rows={rows}
        options={options}
        selected={selected}
        onSelect={onSelect}
      />
      <div ref={scrollRef} data-testid={ids.chartScroll} className="min-w-0 flex-1 overflow-x-auto">
        <GanttCanvas
          view={view}
          rows={rows}
          geometry={geometry}
          options={options}
          selected={selected}
          onSelect={onSelect}
        />
      </div>
    </div>
  );
}
