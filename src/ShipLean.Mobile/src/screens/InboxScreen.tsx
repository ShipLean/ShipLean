import { TabPlaceholder } from '../shell/TabPlaceholder';
import { screenIds } from './testIds';

export interface InboxScreenProps {
  projectKey: string;
  // 'setup' when the setup's first command opened the Inbox with its answer.
  from?: 'setup';
}

// TODO: the Inbox (everything waiting on you, answered from the list) replaces this placeholder.
export function InboxScreen({ projectKey }: InboxScreenProps) {
  return <TabPlaceholder projectKey={projectKey} title="Inbox" testID={screenIds.inbox} />;
}
