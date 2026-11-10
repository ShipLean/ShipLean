import { InboxActionsProvider } from '../../inbox/InboxActions';
import { InboxSheetsProvider } from '../../inbox/InboxSheets';
import type { ProjectSlotProps } from './types';

// The Inbox slot: its actions (the executor on the runner) and the two sheets every screen opens.
export function InboxProvider({ children }: ProjectSlotProps) {
  return (
    <InboxActionsProvider>
      <InboxSheetsProvider>{children}</InboxSheetsProvider>
    </InboxActionsProvider>
  );
}
