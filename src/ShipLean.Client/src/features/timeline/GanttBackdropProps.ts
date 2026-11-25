import type { FeatureTimelineViewDto } from '@shiplean/core/api/views';
import type { TimelineOptions } from './TimelineOptions';
import type { TimelineGeometry } from './chart/timelineGeometry';
import type { TimelineRow } from './chart/timelineRows';

/** What sits under the marks: sprint band, day labels, weekends, row lines, the selected-row band and the now line. */
export interface GanttBackdropProps {
  view: FeatureTimelineViewDto;
  rows: TimelineRow[];
  geometry: TimelineGeometry;
  options: TimelineOptions;
  selected: string | null;
}
