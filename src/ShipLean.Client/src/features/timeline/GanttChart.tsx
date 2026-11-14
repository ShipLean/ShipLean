import { timelineTestIds as ids } from './testIds';
import type { GanttChartProps } from './GanttChartProps';

// TODO: replaced by the drawn chart (scale, rows, bars, brackets, diamonds, review dots, selection). The real chart keeps
// the data-* attributes and the one clickable element per bar.
export function GanttChart({ view, options, selected, onSelect }: GanttChartProps) {
  return (
    <div
      data-testid={ids.chart}
      data-selected={selected ?? undefined}
      data-group={options.group}
      data-zoom={options.zoom}
      data-deps={options.deps}
      data-cp={options.cp}
      data-view={options.view}
    >
      {view.bars.map((bar) => (
        <button
          key={bar.key}
          type="button"
          data-testid={ids.chartBar}
          data-key={bar.key}
          onClick={() => onSelect(bar.key)}
        >
          {bar.key}
        </button>
      ))}
    </div>
  );
}
