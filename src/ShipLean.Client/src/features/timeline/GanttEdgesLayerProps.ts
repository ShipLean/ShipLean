import type { TimelineEdgeDto } from '@shiplean/core/api/views';
import type { BarBox } from './BarBox';
import type { TimelineOptions } from './TimelineOptions';

/** An hour offset h is drawn at originX + h * pxPerHour. */
export interface GanttEdgesLayerProps {
  boxes: BarBox[];
  edges: TimelineEdgeDto[];
  criticalPath: string[];
  options: TimelineOptions;
  finished: boolean;
  pxPerHour: number;
  originX: number;
}
