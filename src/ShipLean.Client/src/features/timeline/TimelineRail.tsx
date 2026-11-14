import { timelineTestIds as ids } from './testIds';
import type { TimelineRailProps } from './TimelineRailProps';

// TODO: replaced by the overview, the not-started list and the selected item's panel.
export function TimelineRail({ options, selected }: TimelineRailProps) {
  return (
    <aside
      data-testid={ids.rail}
      data-selected={selected ?? undefined}
      data-group={options.group}
      data-zoom={options.zoom}
      data-deps={options.deps}
      data-cp={options.cp}
      data-view={options.view}
    />
  );
}
