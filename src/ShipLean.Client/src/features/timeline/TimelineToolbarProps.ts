import type { TimelineOptions } from './TimelineOptions';

export interface TimelineToolbarProps {
  options: TimelineOptions;
  onChange(next: TimelineOptions): void;
}
