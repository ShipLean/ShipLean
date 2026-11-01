import type { WorkItemTypeId } from './refs';

// C#: ShipLean.Contracts.Refs.WorkItemTypeRules.
const STORY_LEVEL: readonly WorkItemTypeId[] = ['story', 'bug', 'action'];

/** Whether the type sits at the level of a story: stories, bugs and action items. */
export const isStoryLevel = (type: WorkItemTypeId): boolean => STORY_LEVEL.includes(type);

const NAMES: Record<WorkItemTypeId, string> = {
  epic: 'Epic',
  feature: 'Feature',
  story: 'User Story',
  bug: 'Bug',
  task: 'Task',
  action: 'Action item',
};

/** The display name of a type. */
export const typeName = (type: WorkItemTypeId): string => NAMES[type];
