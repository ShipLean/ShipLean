import { timelineTestIds as ids } from './testIds';
import type { TimelineJsonProps } from './TimelineJsonProps';

// TODO: replaced by the view in its MCP form with a copy button and a field guide.
export function TimelineJson({ view }: TimelineJsonProps) {
  return <div data-testid={ids.json}>{view.feature.key}</div>;
}
