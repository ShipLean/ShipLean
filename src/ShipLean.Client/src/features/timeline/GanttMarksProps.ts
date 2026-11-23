import type { FeatureTimelineViewDto } from '@shiplean/core/api/views';
import type { TimelineOptions } from './TimelineOptions';
import type { TimelineGeometry } from './chart/timelineGeometry';
import type { TimelineRow } from './chart/timelineRows';

/** The marks: halo, bars, brackets, diamonds, duration labels, review lines and dots, commit ticks. */
export interface GanttMarksProps {
  view: FeatureTimelineViewDto;
  rows: TimelineRow[];
  geometry: TimelineGeometry;
  options: TimelineOptions;
  selected: string | null;
  onSelect(key: string | null): void;
}
