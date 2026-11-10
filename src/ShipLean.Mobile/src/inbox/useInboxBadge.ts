import { useProject } from '../shell/useProject';
import { useInboxActions } from './InboxActions';
import { badgeCount } from './model';
import { useInboxQuery } from './queries';

// The Inbox tab's badge: how many asks wait on you in this project, or null for no badge. It reads the cached Inbox
// less the answers still in the runner, so it drops the moment an answer is given and rises again if it fails.
export function useInboxBadge(projectKey: string): number | null {
  const { project } = useProject(projectKey);
  const inbox = useInboxQuery(project?.id);
  const { pending } = useInboxActions();
  return badgeCount(inbox.data, pending);
}
