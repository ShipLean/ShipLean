import type { WorkItemDto } from '@shiplean/core/api/workItems';
import type { TaskChangesDto, TaskCommitsSectionDto } from '@shiplean/core/api/taskChanges';
import combined from '../../../../../ShipLean.TestSupport/DtoFixtures/taskChanges/changes-combined.json' with { type: 'json' };
import section from '../../../../../ShipLean.TestSupport/DtoFixtures/taskChanges/section.json' with { type: 'json' };
import { bug } from '../../workItem/ct/fixtures';

// The wave-0 wire fixtures, shared with the C# DTO tests.
export const CHANGES: TaskChangesDto = combined.changesCombined as TaskChangesDto;
export const SECTION: TaskCommitsSectionDto = section.section as TaskCommitsSectionDto;

// The Task the fixtures belong to, as the page's by-key read answers it.
export const TASK: WorkItemDto = {
  ...bug,
  id: CHANGES.workItemId,
  key: CHANGES.itemKey,
  type: { id: 'task', name: 'Task' },
  title: CHANGES.itemTitle,
};
