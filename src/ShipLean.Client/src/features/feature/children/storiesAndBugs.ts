import type { ChildSummaryDto } from '../../../api/workItems';

const isClosed = (child: ChildSummaryDto) =>
  child.state.category === 'completed' || child.state.category === 'removed';

/** A Feature's children that are stories or bugs, open ones first (the server's order within each group). */
export function storiesAndBugsOf(children: readonly ChildSummaryDto[]): ChildSummaryDto[] {
  const own = children.filter((child) => child.type.id === 'story' || child.type.id === 'bug');
  return [...own.filter((child) => !isClosed(child)), ...own.filter(isClosed)];
}

/** How many of the stories and bugs are closed (completed or removed), and how many there are. */
export function closedCount(children: readonly ChildSummaryDto[]): {
  closed: number;
  total: number;
} {
  const own = storiesAndBugsOf(children);
  return { closed: own.filter(isClosed).length, total: own.length };
}
