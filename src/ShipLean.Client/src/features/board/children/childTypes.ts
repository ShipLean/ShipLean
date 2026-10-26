import type { WorkItemTypeId } from '../../../api/refs';
import type { Level } from '../../../routes';

/** What a card on each board can be given as a child from its list: Features take stories and bugs, Epics take Features. */
export const CHILD_TYPES: Record<Level, { type: WorkItemTypeId; label: string }[]> = {
  stories: [],
  features: [
    { type: 'story', label: 'User story' },
    { type: 'bug', label: 'Bug' },
  ],
  epics: [{ type: 'feature', label: 'Feature' }],
};
