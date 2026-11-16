import type { FeatureTimelineViewDto } from '@shiplean/core/api/views';
import type { TimelineOptions } from './TimelineOptions';

export interface TimelineRailProps {
  view: FeatureTimelineViewDto;
  options: TimelineOptions;
  selected: string | null;
  onSelect(key: string | null): void;
}
