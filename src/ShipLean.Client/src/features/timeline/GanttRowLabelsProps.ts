import type { FeatureTimelineViewDto } from '@shiplean/core/api/views';
import type { TimelineOptions } from './TimelineOptions';
import type { TimelineRow } from './chart/timelineRows';

/** The chart's left column: one label per row, in the rows' order. */
export interface GanttRowLabelsProps {
  view: FeatureTimelineViewDto;
  rows: TimelineRow[];
  options: TimelineOptions;
  selected: string | null;
  onSelect(key: string | null): void;
}
