import { useEffect, useRef } from 'react';
import type { WorkItemDto } from '../../../../api/workItems';
import { useMe } from '../../../shell/useShellQueries';
import { rememberRecent } from './recentItems';

// Opening an item (its page or the modal mounts the form, once per item) puts it first in this user's recent items.
// A newer title, state or parent for the same item replaces the stored copy without reordering.
export function useRememberItem(projectKey: string, item: WorkItemDto) {
  const userId = useMe().data?.id;
  const fronted = useRef<string | null>(null);
  const { key, title, type, state, parent } = item;
  const parentKey = parent?.key ?? null;
  useEffect(() => {
    if (userId === undefined) return;
    const entry = {
      key,
      title,
      type: type.id,
      state: state.name,
      stateCategory: state.category,
      parentPath: parentKey === null ? [] : [parentKey],
      projectKey,
    };
    const opened = `${userId}:${key}`;
    rememberRecent(userId, entry, { moveToFront: fronted.current !== opened });
    fronted.current = opened;
  }, [userId, projectKey, key, title, type.id, state.name, state.category, parentKey]);
}
