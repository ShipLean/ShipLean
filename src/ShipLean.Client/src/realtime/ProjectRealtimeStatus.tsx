import { useQuery } from '@tanstack/react-query';
import { ReconnectBanner } from '../components/ReconnectBanner';
import { realtimeTestIds } from '../features/realtime/testIds';
import { fetchMembers, projectMembersKey } from '../queries/shared';
import type { RouteId } from '../routes';
import { LiveDot } from './LiveDot';
import { PresenceStack } from './PresenceStack';
import { realtime } from './realtime';
import { useConnectionState } from './useConnectionState';
import { useMeId } from './useMeId';
import { useProjectByKey } from './useProjectByKey';
import { useRefreshMembersOnArrival } from './useRefreshMembersOnArrival';
import { useScreenUserIds } from './useScreenUserIds';

// The Live dot and the stack of who else is on this screen of the project, and the reconnect banner while the
// connection is down.
export function ProjectRealtimeStatus({
  routeId,
  projectKey,
}: {
  routeId: RouteId;
  projectKey: string;
}) {
  const state = useConnectionState();
  const meId = useMeId();
  const projectId = useProjectByKey(projectKey).data?.id;
  const members = useQuery({
    queryKey: projectMembersKey(projectId ?? ''),
    queryFn: () => fetchMembers(projectId ?? ''),
    enabled: projectId !== undefined,
  }).data;
  const userIds = useScreenUserIds(routeId);
  useRefreshMembersOnArrival(projectId, userIds);
  return (
    <>
      <span className="flex items-center gap-3">
        <PresenceStack userIds={userIds} members={members ?? []} meId={meId} />
        <LiveDot state={state} />
      </span>
      {state === 'reconnecting' ? (
        <div
          data-testid={realtimeTestIds.banner}
          className="fixed top-[calc(var(--bar-height)+8px)] right-4 z-50"
        >
          <ReconnectBanner state="reconnecting" onRetry={realtime.retryNow} />
        </div>
      ) : null}
    </>
  );
}
