import type { ProjectDto } from '@shiplean/core/api/projects';
import { AgentRunsPanel } from '../features/inspection/AgentRunsPanel';
import { inspectionTestIds } from '../features/inspection/testIds';
import { useWorkItem } from '../features/workItem/workItemApi';

export interface AgentRunsSlotProps {
  project: ProjectDto;
  itemKey: string;
}

// The Agent runs section on the item page, above Discussion (under Changes once SHIP-1460 builds it). The flag comes
// from the work item the page already loaded, so a project with Inspection off makes no extra request.
export function AgentRunsSlot({ project, itemKey }: AgentRunsSlotProps) {
  const item = useWorkItem(project.id, itemKey);
  return (
    <div data-testid={inspectionTestIds.agentRunsSlot} className="contents">
      <AgentRunsPanel
        project={project}
        itemKey={itemKey}
        enabled={item.data?.inspectionEnabled === true}
      />
    </div>
  );
}
