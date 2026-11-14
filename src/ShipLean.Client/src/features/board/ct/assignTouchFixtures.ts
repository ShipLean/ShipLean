import type { ChildSummaryDto } from '@shiplean/core/api/workItems';
import { TOM } from './fixtures';

/** The project's members the Assign sheet lists (CT only). */
export const assignMembers = [TOM, { ...TOM, id: 'u-ann', name: 'Ann Lee', initials: 'AL' }];

const child = (key: string, state: string, category: ChildSummaryDto['state']['category']) =>
  ({
    id: `id-${key}`,
    key,
    type: { id: 'story', name: 'User Story' },
    title: `Title of ${key}`,
    state: { id: `s-${state}`, name: state, category },
    assignee: TOM,
    points: 3,
  }) satisfies ChildSummaryDto;

/** The children the list shows (CT only). */
export const assignChildren = [
  child('SHIP-141', 'Active', 'inProgress'),
  child('SHIP-142', 'New', 'proposed'),
  child('SHIP-143', 'Closed', 'completed'),
];
