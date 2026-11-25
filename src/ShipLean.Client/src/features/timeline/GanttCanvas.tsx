import { GanttBackdrop } from './GanttBackdrop';
import type { GanttCanvasProps } from './GanttCanvasProps';
import { GanttEdgesLayer } from './GanttEdgesLayer';
import { GanttMarks } from './GanttMarks';

/** The chart's SVG shell: a click on empty space clears the selection; backdrop, marks and edges draw in that order. */
export function GanttCanvas({
  view,
  rows,
  geometry,
  options,
  selected,
  onSelect,
}: GanttCanvasProps) {
  return (
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
      <GanttBackdrop
        view={view}
        rows={rows}
        geometry={geometry}
        options={options}
        selected={selected}
      />
      <GanttMarks
        view={view}
        rows={rows}
        geometry={geometry}
        options={options}
        selected={selected}
        onSelect={onSelect}
      />
      <GanttEdgesLayer
        boxes={geometry.boxes}
        marks={geometry.marks}
        edges={view.edges}
        criticalPath={view.criticalPath}
        options={options}
        finished={view.finished}
        pxPerHour={geometry.pxPerHour}
        originX={0}
      />
    </svg>
  );
}
