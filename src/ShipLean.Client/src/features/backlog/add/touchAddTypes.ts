import type { WorkItemTypeId } from '@shiplean/core/api/refs';
import { typeName } from '@shiplean/core/api/workItemTypes';

// What the phone add rows share: a Story / Bug switch (Action item stays on the desktop select; three segments overflow
// 390px) and the help line under it.
export const TOUCH_ADD_TYPE_IDS: readonly WorkItemTypeId[] = ['story', 'bug'];

export const TOUCH_ADD_HELP = 'Adds at the top with the next key and stays open for the next one.';

export function touchAddTypeOptions(
  available: readonly WorkItemTypeId[],
): { value: WorkItemTypeId; label: string }[] {
  return available
    .filter((id) => TOUCH_ADD_TYPE_IDS.includes(id))
    .map((id) => ({ value: id, label: id === 'story' ? 'Story' : typeName(id) }));
}
