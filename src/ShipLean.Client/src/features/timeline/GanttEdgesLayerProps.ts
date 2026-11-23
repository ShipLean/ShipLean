import type { TimelineEdgeDto } from '@shiplean/core/api/views';
import type { BarBox } from './BarBox';
import type { TimelineOptions } from './TimelineOptions';
import type { Mark } from './chart/timelineGeometry';

/** An hour offset h is drawn at originX + h * pxPerHour. */
export interface GanttEdgesLayerProps {
  boxes: BarBox[];
  /** The geometry's marks with their review dots, so labels can keep clear of every markBox and durationLabelBox. */
  marks: Mark[];
  edges: TimelineEdgeDto[];
  criticalPath: string[];
  options: TimelineOptions;
  finished: boolean;
  pxPerHour: number;
  originX: number;
}
