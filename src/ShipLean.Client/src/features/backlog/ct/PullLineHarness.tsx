import { ToastHost } from '../../../components/ToastHost';
import type { ProjectDto } from '../../../api/projects';
import { backlogKey, useBacklog } from '../backlogApi';
import { BulkOutcomeHost } from '../bulk/BulkOutcomeHost';
import { AddBelowProvider } from '../add/AddBelowProvider';
import { BulkRunProvider } from '../bulk/BulkRunProvider';
import { ListCommandsProvider } from '../commands/listCommands';
import { forecastMarks } from '../planning/forecastMarks';
import { PlanningDnd } from '../planning/PlanningDnd';
import { PlanningSections } from '../planning/PlanningSections';
import { useSectionCollapse } from '../planning/useSectionCollapse';
import { SelectionProvider } from '../selection/SelectionProvider';

const NO_MARKS: ReturnType<typeof forecastMarks> = new Map();
const NONE: ReadonlySet<string> = new Set();

// CT-only: Planning's sections with the forecast and filtering set from outside, and the bulk outcome host that
// reports a stale row, since the search and the check queue that turn them on are other tasks'.
export function PullLineHarness({
  project,
  forecast = true,
  filtering = false,
}: {
  project: ProjectDto;
  forecast?: boolean;
  filtering?: boolean;
}) {
  const query = {
    projectId: project.id,
    level: 'stories',
    mode: 'planning',
    filter: '',
    includeCompleted: false,
  } as const;
  const backlog = useBacklog(query);
  const collapse = useSectionCollapse(backlog.data?.sections ?? []);
  if (!backlog.isSuccess) return null;
  const view = backlog.data;
  return (
    <ListCommandsProvider>
      <BulkRunProvider
        projectId={project.id}
        queryKey={backlogKey(project.id, 'stories', 'planning', '', false)}
      >
        <SelectionProvider>
          <AddBelowProvider projectId={project.id} projectKey={project.key}>
            <ToastHost />
            <BulkOutcomeHost projectId={project.id} />
            <PlanningDnd
              sections={view.sections}
              filtering={filtering}
              velocity={view.velocity}
              onMove={() => undefined}
            >
              {({ tableRef, dropLine, onKeyMove }) => (
                <PlanningSections
                  sections={view.sections}
                  velocity={view.velocity}
                  forecast={forecast}
                  marks={NO_MARKS}
                  projectId={project.id}
                  projectKey={project.key}
                  adding={false}
                  onCloseAdd={() => undefined}
                  flashing={NONE}
                  collapse={collapse}
                  filtering={filtering}
                  tableRef={tableRef}
                  dropLine={dropLine}
                  onKeyMove={onKeyMove}
                />
              )}
            </PlanningDnd>
          </AddBelowProvider>
        </SelectionProvider>
      </BulkRunProvider>
    </ListCommandsProvider>
  );
}
