import { QuickSearchDialogHarness } from '../../../ct/QuickSearchDialogHarness';
import { useRecentSource } from '../../useRecentSource';

// The dialog with the real recent source in it.
export function RecentHarness() {
  return <QuickSearchDialogHarness results={useRecentSource()} />;
}
