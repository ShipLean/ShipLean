import type { FeatureTimelineViewDto } from '@shiplean/core/api/views';
import type { TimelineOptions } from './TimelineOptions';
import type { TimelineGeometry } from './chart/timelineGeometry';
import type { TimelineRow } from './chart/timelineRows';

/** The chart's SVG: the rows and the geometry GanttChart computed once. */
export interface GanttCanvasProps {
  view: FeatureTimelineViewDto;
  rows: TimelineRow[];
  geometry: TimelineGeometry;
  options: TimelineOptions;
  selected: string | null;
  onSelect(key: string | null): void;
}
