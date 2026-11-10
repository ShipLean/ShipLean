import { useEffect, useRef } from 'react';
import { usePending } from '../../actions/ActionRunnerProvider';
import { useInvalidateWorkItem } from '../../workItem/queries';
import { useInvalidateFeatures } from '../queries';

// When a Features change to this Feature has been sent, the overview and everything read about the Feature is read
// again. A change already sent when the row opens refreshes nothing: the queries load fresh then.
export function useRefreshOnSent(projectId: string, itemKey: string, itemId: string): void {
  const pending = usePending((p) => p.action.area === 'features' && p.action.itemId === itemId);
  const handled = useRef<Set<string> | null>(null);
  const invalidateFeatures = useInvalidateFeatures();
  const invalidateItem = useInvalidateWorkItem();
  useEffect(() => {
    const sent = pending.filter((p) => p.state === 'sent').map((p) => p.id);
    if (handled.current === null) {
      handled.current = new Set(sent);
      return;
    }
    const fresh = sent.filter((id) => !(handled.current as Set<string>).has(id));
    if (fresh.length === 0) return;
    for (const id of fresh) handled.current.add(id);
    void invalidateFeatures(projectId);
    void invalidateItem(projectId, itemKey, itemId);
  }, [pending, projectId, itemKey, itemId, invalidateFeatures, invalidateItem]);
}
