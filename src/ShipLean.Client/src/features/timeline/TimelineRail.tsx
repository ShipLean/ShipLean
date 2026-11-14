import { findRailItem } from './rail/railLookup';
import { RailJsonGuide } from './rail/RailJsonGuide';
import { RailOverview } from './rail/RailOverview';
import { RailStory, RailTask } from './rail/RailItem';
import { timelineTestIds as ids } from './testIds';
import type { TimelineRailProps } from './TimelineRailProps';

/** The right rail: the field guide in JSON mode, the selected item's facts, or the overview. */
export function TimelineRail({ view, options, selected, onSelect }: TimelineRailProps) {
  const item = options.view === 'json' ? null : findRailItem(view, selected);
  let body;
  if (options.view === 'json') body = <RailJsonGuide />;
  else if (item?.kind === 'task')
    body = <RailTask view={view} bar={item.bar} onSelect={onSelect} />;
  else if (item?.kind === 'story')
    body = <RailStory view={view} story={item.story} onSelect={onSelect} />;
  else body = <RailOverview view={view} />;
  return (
    <aside
      data-testid={ids.rail}
      data-selected={selected ?? undefined}
      data-group={options.group}
      data-zoom={options.zoom}
      data-deps={options.deps}
      data-cp={options.cp}
      data-view={options.view}
      className="overflow-auto border-l border-edge bg-panel"
    >
      {body}
    </aside>
  );
}
