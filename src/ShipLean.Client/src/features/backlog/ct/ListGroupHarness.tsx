import { ToastHost } from '../../../components/ToastHost';
import type { ProjectDto } from '../../../api/projects';
import { backlogKey, useBacklog } from '../backlogApi';
import { BulkRunProvider } from '../bulk/BulkRunProvider';
import { ListCommandsProvider } from '../commands/listCommands';
import { ListDnd } from '../list/ListDnd';
import { ListTable } from '../list/ListTable';
import { SelectionProvider } from '../selection/SelectionProvider';
import { SelectPicker } from './SelectPicker';

const NONE: ReadonlySet<string> = new Set();

// CT-only: the list's drag and drop over a selection a test sets with SelectPicker, since the row checkboxes are
// another task's.
export function ListGroupHarness({ project }: { project: ProjectDto }) {
  const backlog = useBacklog({
    projectId: project.id,
    level: 'stories',
    mode: 'list',
    filter: '',
    includeCompleted: false,
  });
  if (!backlog.isSuccess) return null;
  const rows = backlog.data.rows;
  return (
    <ListCommandsProvider>
      <BulkRunProvider
        projectId={project.id}
        queryKey={backlogKey(project.id, 'stories', 'list', '', false)}
      >
        <SelectionProvider>
          <ToastHost />
          <SelectPicker rows={rows} />
          <ListDnd rows={rows} filtering={false} onReorder={() => undefined}>
            <ListTable
              rows={rows}
              columns={['rank', 'key', 'title', 'points']}
              projectId={project.id}
              projectKey={project.key}
              adding={false}
              onCloseAdd={() => undefined}
              dragDisabled={false}
              flashing={NONE}
              filtered={false}
            />
          </ListDnd>
        </SelectionProvider>
      </BulkRunProvider>
    </ListCommandsProvider>
  );
}
