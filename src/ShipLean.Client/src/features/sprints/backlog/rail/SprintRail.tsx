import type { WorkItemDto } from '../../../../api/workItems';
import { useMe } from '../../../shell/useShellQueries';
import { useProjectMembers } from '../../../workItem/workItemApi';
import { sprintsTestIds as ids } from '../../testIds';
import type { SprintStory } from '../sprintStories';
import { InProgressNow } from './InProgressNow';
import { PointsByState } from './PointsByState';
import { railSummary } from './railSummary';
import { ReadyToPickUp } from './ReadyToPickUp';

interface SprintRailProps {
  projectId: string;
  stories: SprintStory[];
  unparented: WorkItemDto[];
}

/** Beside the table: what is ready to pick up, who is on what, and the points by state category. */
export function SprintRail({ projectId, stories, unparented }: SprintRailProps) {
  const members = useProjectMembers(projectId).data ?? [];
  const meId = useMe().data?.id;
  const summary = railSummary(stories, unparented);
  return (
    <aside
      data-testid={ids.rail}
      className="grid w-64 flex-none content-start gap-4 rounded-md border border-edge bg-panel p-3"
    >
      <ReadyToPickUp ready={summary.ready} />
      <InProgressNow members={members} meId={meId} activeByUser={summary.activeByUser} />
      <PointsByState points={summary.points} />
    </aside>
  );
}
