import type { ProjectDto } from '@shiplean/core/api/projects';
import { CommitsSection } from '../features/taskChanges/CommitsSection';
import { useAddTaskCommit, useTaskCommitsSection } from '../features/taskChanges/taskChangesApi';
import { useWideScreen } from '../features/taskChanges/useWideScreen';
import { readViewed, viewedCount } from '../features/taskChanges/viewedStore';
import { useWorkItem } from '../features/workItem/workItemApi';

export interface ChangesSlotProps {
  project: ProjectDto;
  itemKey: string;
}

// The Changes section of a Task or Bug page. Under 900px it renders nothing and makes no request (design D2): the width
// check is its own component so no query hook runs before it.
export function ChangesSlot({ project, itemKey }: ChangesSlotProps) {
  const wide = useWideScreen();
  return wide ? <WideChangesSection project={project} itemKey={itemKey} /> : null;
}

function WideChangesSection({ project, itemKey }: ChangesSlotProps) {
  const item = useWorkItem(project.id, itemKey);
  const workItemId = item.data?.id;
  const section = useTaskCommitsSection(project.id, workItemId);
  const add = useAddTaskCommit(project.id, workItemId ?? '');
  if (workItemId === undefined || section.data === undefined) return null;
  return (
    <CommitsSection
      projectKey={project.key}
      itemKey={itemKey}
      section={section.data}
      viewedCount={viewedCount(
        readViewed(workItemId),
        section.data.headCommitId,
        section.data.filePaths,
      )}
      onAddCommit={async (repo, sha) => {
        await add.mutateAsync({ repo, sha });
      }}
    />
  );
}
