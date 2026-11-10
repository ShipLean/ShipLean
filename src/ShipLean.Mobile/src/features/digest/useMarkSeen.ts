import { useRouter } from 'expo-router';
import { useRef } from 'react';
import { useActionRunner } from '../../actions/ActionRunnerProvider';
import { UNDO } from '../../actions/undo';
import { links } from '../../links';
import { useFeaturesActions } from '../FeaturesActions';
import { useInvalidateFeatures } from '../queries';

// Marks the digest seen once, then goes back to the Features tab. Once it has been sent the overview and digest are read
// again, so the tab's banner counts from now. The screen is gone by then, so the watch outlives it.
export function useMarkSeen(projectId: string, projectKey: string): () => void {
  const router = useRouter();
  const runner = useActionRunner();
  const invalidate = useInvalidateFeatures();
  const { dispatch } = useFeaturesActions();
  const done = useRef(false);
  return () => {
    if (done.current) return;
    done.current = true;
    const batchId = dispatch(
      { area: 'features', kind: 'markDigestSeen', itemId: null, key: null, projectId },
      { undo: UNDO.none },
    );
    const stop = runner.subscribe(() => {
      const mine = runner.pending().filter((p) => p.batchId === batchId);
      if (mine.length === 0) {
        stop();
        return;
      }
      if (mine.some((p) => p.state === 'sent')) {
        void invalidate(projectId);
        stop();
      } else if (mine.some((p) => p.state === 'failed')) stop();
    });
    router.dismissTo(links.features(projectKey));
  };
}
