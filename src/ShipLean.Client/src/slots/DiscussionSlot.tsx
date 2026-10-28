import type { ProjectDto } from '../api/projects';
import { pagesTestIds } from '../features/pages/testIds';
import { AgentPanel } from '../features/agentPanel/AgentPanel';
import { DiscussionSection } from '../features/workItem/discussion/DiscussionSection';

export interface DiscussionSlotProps {
  project: ProjectDto;
  itemKey: string;
}

// The work item's Agent panel (quiet when the project has no agent) above its discussion. Comments save at once and never touch the form's draft.
export function DiscussionSlot({ project, itemKey }: DiscussionSlotProps) {
  return (
    <div data-testid={pagesTestIds.slotDiscussion} className="grid gap-4">
      <AgentPanel project={project} itemKey={itemKey} />
      <DiscussionSection project={project} itemKey={itemKey} />
    </div>
  );
}
