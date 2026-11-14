import { timelineTestIds as ids } from './testIds';
import type { TimelineToolbarProps } from './TimelineToolbarProps';

// TODO: replaced by the grouping, zoom, dependency, critical path and view controls and the legend.
export function TimelineToolbar({ options }: TimelineToolbarProps) {
  return (
    <div
      data-testid={ids.toolbar}
      data-group={options.group}
      data-zoom={options.zoom}
      data-deps={options.deps}
      data-cp={options.cp}
      data-view={options.view}
    />
  );
}
