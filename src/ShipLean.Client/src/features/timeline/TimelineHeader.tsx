import { timelineTestIds as ids } from './testIds';
import type { TimelineHeaderProps } from './TimelineHeaderProps';

// TODO: replaced by the back link, title, state and epic of the Feature.
export function TimelineHeader({ view }: TimelineHeaderProps) {
  return <header data-testid={ids.header}>{view.feature.key}</header>;
}
