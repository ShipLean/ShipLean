import type { TimelineOptions } from './TimelineOptions';

export interface TimelineToolbarProps {
  options: TimelineOptions;
  /** The Feature is Done: only then is there a critical path to show. */
  finished: boolean;
  onChange(next: TimelineOptions): void;
}
