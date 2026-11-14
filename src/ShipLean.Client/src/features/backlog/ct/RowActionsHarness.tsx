import { useLocation } from 'react-router-dom';
import { ToastHost } from '../../../components/ToastHost';
import type { ProjectDto } from '@shiplean/core/api/projects';
import { backlogKey, useBacklog } from '../backlogApi';
import { BulkRunProvider } from '../bulk/BulkRunProvider';
import { ListCommandsProvider } from '../commands/listCommands';
import { SelectionProvider } from '../selection/SelectionProvider';
import { TouchSelectModeProvider } from '../touch/selectMode';
import { TouchRowList } from '../touch/TouchRowList';
import { rowActionsHarnessIds } from './rowActionsHarnessIds';

const NONE: ReadonlySet<string> = new Set();

function Rows({ project, tree }: { project: ProjectDto; tree: boolean }) {
  const backlog = useBacklog({
    projectId: project.id,
    level: 'stories',
    mode: 'list',
    filter: '',
    includeCompleted: false,
  });
  const { pathname, search } = useLocation();
  if (!backlog.isSuccess) return null;
  const list = (
    <TouchRowList
      rows={backlog.data.rows}
      projectKey={project.key}
      handles={false}
      flashing={NONE}
      filtered={false}
    />
  );
  return (
    <>
      <output data-testid={rowActionsHarnessIds.address}>{pathname + search}</output>
      {tree ? list : <SelectionProvider>{list}</SelectionProvider>}
    </>
  );
}

// CT-only: touch rows with the row ⋯ under the bulk runner and the touch Select mode, as List mounts them. `tree`
// leaves the SelectionProvider out, as Tree mode does (no Points, no Select).
export function RowActionsHarness({
  project,
  tree = false,
}: {
  project: ProjectDto;
  tree?: boolean;
}) {
  return (
    <ListCommandsProvider>
      <TouchSelectModeProvider>
        <BulkRunProvider
          projectId={project.id}
          queryKey={backlogKey(project.id, 'stories', 'list', '', false)}
        >
          <ToastHost />
          <Rows project={project} tree={tree} />
        </BulkRunProvider>
      </TouchSelectModeProvider>
    </ListCommandsProvider>
  );
}
