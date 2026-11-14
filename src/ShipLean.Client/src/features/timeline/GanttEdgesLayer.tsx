import { timelineTestIds as ids } from './testIds';
import type { GanttEdgesLayerProps } from './GanttEdgesLayerProps';

// TODO: replaced by the dependency arrows, wait labels and critical path outline, drawn inside the chart's SVG.
export function GanttEdgesLayer({ boxes }: GanttEdgesLayerProps) {
  return <g data-testid={ids.edges} data-boxes={boxes.length} />;
}
