import type { FC } from 'react';
import { useActionRunner } from '../../actions/ActionRunnerProvider';
import type { Pending } from '../../actions/types';
import { Banner } from '../../ui/Banner';
import { refusedChangeId } from '../testIds';

export interface RefusedChangeProps {
  pending: Pending;
  onDismiss: () => void;
}

// One refused change: what it was, the runner's reason, and Retry when the runner can send it again, else Dismiss.
export const RefusedChange: FC<RefusedChangeProps> = ({ pending, onDismiss }) => {
  const runner = useActionRunner();
  return (
    <Banner
      tone="danger"
      testID={refusedChangeId(pending.id)}
      text={[`Didn't send “${runner.describe(pending.action).text}”.`, pending.error]
        .filter((part) => part !== null)
        .join(' ')}
      action={
        pending.retryable
          ? { label: 'Retry', onPress: () => runner.retry(pending.id) }
          : { label: 'Dismiss', onPress: onDismiss }
      }
    />
  );
};
