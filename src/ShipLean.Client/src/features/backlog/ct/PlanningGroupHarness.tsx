import { useState } from 'react';
import { ToastHost } from '../../../components/ToastHost';
import type { ProjectDto } from '../../../api/projects';
import { backlogKey, useBacklog } from '../backlogApi';
import { BulkRunProvider } from '../bulk/BulkRunProvider';
import { ListCommandsProvider } from '../commands/listCommands';
import { forecastMarks } from '../planning/forecastMarks';
import { PlanningDnd } from '../planning/PlanningDnd';
import { PlanningSections } from '../planning/PlanningSections';
import { useSectionCollapse } from '../planning/useSectionCollapse';
import { SelectionProvider } from '../selection/SelectionProvider';
import { AddBelowProvider } from '../add/AddBelowProvider';
import { SelectPicker } from './SelectPicker';

const NO_MARKS: ReturnType<typeof forecastMarks> = new Map();
const NONE: ReadonlySet<string> = new Set();

// CT-only: Planning's drag and drop and sections over a selection a test sets with SelectPicker, since the row
// checkboxes are another task's. `moves` lists the rows moved alone, which PlanningMode would write one at a time.
export function PlanningGroupHarness({
  project,
  filtering = false,
}: {
  project: ProjectDto;
  filtering?: boolean;
}) {
  const [moves, setMoves] = useState<string[]>([]);
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
  const rows = view.sections.flatMap((section) => section.rows);
  return (
    <ListCommandsProvider>
      <BulkRunProvider
        projectId={project.id}
        queryKey={backlogKey(project.id, 'stories', 'planning', '', false)}
      >
        <SelectionProvider>
          <AddBelowProvider projectId={project.id} projectKey={project.key}>
            <ToastHost />
            <SelectPicker rows={rows} />
            <span data-testid="harness-moves">{moves.join(',')}</span>
            <PlanningDnd
              sections={view.sections}
              filtering={filtering}
              velocity={view.velocity}
              onMove={(row) => setMoves((current) => [...current, row.key])}
            >
              {({ tableRef, dropLine, onKeyMove }) => (
                <PlanningSections
                  sections={view.sections}
                  velocity={view.velocity}
                  forecast={false}
                  marks={NO_MARKS}
                  projectId={project.id}
                  projectKey={project.key}
                  adding={false}
                  onCloseAdd={() => undefined}
                  flashing={NONE}
                  collapse={collapse}
                  filtering={filtering}
                  addingOff={filtering}
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
