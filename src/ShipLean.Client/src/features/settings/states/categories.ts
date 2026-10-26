import { STATE_CATEGORIES, type StateCategory, type WorkItemTypeId } from '../../../api/refs';

export const CATEGORY_LABELS: Record<StateCategory, string> = {
  proposed: 'Proposed',
  inProgress: 'In progress',
  resolved: 'Resolved',
  completed: 'Completed',
  removed: 'Removed',
};

export const CATEGORIES = STATE_CATEGORIES;

export const parseCategory = (value: string): StateCategory =>
  STATE_CATEGORIES.find((c) => c === value) ?? 'proposed';

export const TYPE_LABELS: Record<WorkItemTypeId, string> = {
  epic: 'Epic',
  feature: 'Feature',
  story: 'User Story',
  bug: 'Bug',
  task: 'Task',
};
