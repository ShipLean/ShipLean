import type { ProjectDto } from '../api/projects';
import { RetroActionDialogProvider } from '../features/retroShared/actionDialog/RetroActionDialogProvider';
import { ActionWhy } from '../features/retros/actionItem/ActionWhy';
import { WorkItemRetroSection } from '../features/retros/workItem/WorkItemRetroSection';
import { useWorkItem } from '../features/workItem/workItemApi';

export interface WorkItemRetroSlotProps {
  project: ProjectDto;
  itemKey: string;
}

// The work item's retrospective section. It waits for the item the page already loads, then reads that item's retros.
export function WorkItemRetroSlot({ project, itemKey }: WorkItemRetroSlotProps) {
  const item = useWorkItem(project.id, itemKey).data;
  if (!item) return null;
  return (
    <RetroActionDialogProvider projectId={project.id}>
      <ActionWhy
        projectId={project.id}
        projectKey={project.key}
        workItemId={item.id}
        itemType={item.type.id}
      />
      <WorkItemRetroSection projectId={project.id} workItemId={item.id} />
    </RetroActionDialogProvider>
  );
}
