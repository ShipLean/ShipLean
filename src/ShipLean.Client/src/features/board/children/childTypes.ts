import { WORK_ITEM_TYPE_IDS, type WorkItemTypeId } from '../../../api/refs';
import { isStoryLevel, typeName } from '../../../api/workItemTypes';
import type { Level } from '../../../routes';

const storyLevel = WORK_ITEM_TYPE_IDS.filter(isStoryLevel).map((type) => ({
  type,
  label: typeName(type),
}));

/** What a card on each board can be given as a child from its list: Features take story-level items (stories, bugs, action items), Epics take Features. */
export const CHILD_TYPES: Record<Level, { type: WorkItemTypeId; label: string }[]> = {
  stories: [],
  features: storyLevel,
  epics: [{ type: 'feature', label: 'Feature' }],
};
