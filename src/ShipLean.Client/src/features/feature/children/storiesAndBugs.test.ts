import { expect, test } from 'vitest';
import type { WorkItemTypeId } from '@shiplean/core/api/refs';
import type { ChildSummaryDto } from '@shiplean/core/api/workItems';
import { closedCount, storiesAndBugsOf } from './storiesAndBugs';

const child = (key: string, type: WorkItemTypeId, category = 'proposed') =>
  ({ key, type: { id: type }, state: { category } }) as unknown as ChildSummaryDto;

test('storiesAndBugsOf_WhenActionItemsPresent_ShouldListThemWithStoriesAndBugs', () => {
  const children = [
    child('A', 'action', 'completed'),
    child('B', 'task'),
    child('C', 'story'),
    child('D', 'action'),
  ];
  expect(storiesAndBugsOf(children).map((c) => c.key)).toEqual(['C', 'D', 'A']);
  expect(closedCount(children)).toEqual({ closed: 1, total: 3 });
});
