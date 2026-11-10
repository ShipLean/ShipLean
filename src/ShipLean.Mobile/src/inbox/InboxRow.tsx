import type { InboxItemDto } from '@shiplean/core/api/inbox';
import { View } from 'react-native';
import type { PendingAnswer } from './actions';
import { inboxRowId } from './testIds';

export interface InboxRowProps {
  item: InboxItemDto;
  // This item's answers still in the runner (held, queued, sending, uncertain or failed).
  pending: PendingAnswer[];
  // A Story's row under its Feature's.
  indented: boolean;
  // The item's other asks opened in place.
  expanded: boolean;
  onToggleExpanded: () => void;
  // A tap on the row, not on a button: opens links.item(projectKey, item.key).
  onOpenItem: () => void;
}

// TODO: the Inbox row (kind, ask, who asked, what waits on it, answer buttons, more asks in place) replaces this stub.
export function InboxRow({ item, indented }: InboxRowProps) {
  return <View testID={inboxRowId(item.key)} data-indented={indented} />;
}
