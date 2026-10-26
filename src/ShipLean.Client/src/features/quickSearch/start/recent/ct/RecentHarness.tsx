import { useMe } from '../../../../shell/useShellQueries';
import { QuickSearchDialogHarness } from '../../../ct/QuickSearchDialogHarness';
import { useRecentSource } from '../../useRecentSource';
import { recentHarnessIds } from './harnessIds';

// The dialog with the real recent source in it, and the user id the source waits for.
export function RecentHarness() {
  const userId = useMe().data?.id;
  return (
    <>
      <span data-testid={recentHarnessIds.userId}>{userId ?? ''}</span>
      <QuickSearchDialogHarness results={useRecentSource()} />
    </>
  );
}
