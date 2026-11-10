import { useState, type FC } from 'react';
import { View } from 'react-native';
import { useActionRunner, usePending } from '../../actions/ActionRunnerProvider';
import type { Pending } from '../../actions/types';
import { Banner } from '../../ui/Banner';
import { refusedChangeId, refusedChangesId } from '../testIds';

export interface RefusedChangesProps {
  itemId: string;
}

// A tapped or typed change to this item that didn't send (voice D18: a banner on the item), worded by its executor
// with the runner's reason, such as a 409's "Changed on the server. Open ‹key› to check." The screen has read the item
// again by then (WorkItemActionsProvider), so what it shows is the server's. Retry when the runner can send it again,
// otherwise Dismiss. Voice changes have their own banner (VoiceSendFailedBanner).
export const RefusedChanges: FC<RefusedChangesProps> = ({ itemId }) => {
  const runner = useActionRunner();
  const [dismissed, setDismissed] = useState<ReadonlySet<string>>(new Set());
  const refused = usePending(
    (p: Pending) =>
      p.action.area === 'workItem' &&
      p.action.itemId === itemId &&
      p.origin !== 'voice' &&
      p.state === 'failed' &&
      p.undoOutcome === null &&
      !dismissed.has(p.id),
  );
  if (refused.length === 0) return null;
  return (
    <View testID={refusedChangesId}>
      {refused.map((p) => (
        <Banner
          key={p.id}
          tone="danger"
          testID={refusedChangeId(p.id)}
          text={[`Didn't send “${runner.describe(p.action).text}”.`, p.error]
            .filter((part) => part !== null)
            .join(' ')}
          action={
            p.retryable
              ? { label: 'Retry', onPress: () => runner.retry(p.id) }
              : {
                  label: 'Dismiss',
                  onPress: () => setDismissed(new Set([...dismissed, p.id])),
                }
          }
        />
      ))}
    </View>
  );
};
