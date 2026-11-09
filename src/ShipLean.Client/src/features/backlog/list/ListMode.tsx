import type { ProjectDto } from '@shiplean/core/api/projects';
import { BulkRunProvider } from '../bulk/BulkRunProvider';
import { ListCommandsProvider } from '../commands/listCommands';
import { useBacklogFilter } from '../filter/useBacklogFilter';
import { ListScreen } from './ListScreen';

// The Stories backlog in list mode. The commands and the bulk runner every part of the screen shares wrap
// ListScreen, which is the screen itself and mounts the selection.
export function ListMode({ project }: { project: ProjectDto }) {
  const state = useBacklogFilter(project, 'list');
  return (
    <ListCommandsProvider>
      <BulkRunProvider projectId={project.id} queryKey={state.queryKey}>
        <ListScreen project={project} state={state} />
      </BulkRunProvider>
    </ListCommandsProvider>
  );
}
