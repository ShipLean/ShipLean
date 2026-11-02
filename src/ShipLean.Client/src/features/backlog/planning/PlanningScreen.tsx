import { useMemo, useState } from 'react';
import type { ProjectDto } from '../../../api/projects';
import type { BacklogViewDto } from '../../../api/views';
import { AddManyButton } from '../add/AddManyButton';
import { PasteHost } from '../add/PasteHost';
import { BulkBar } from '../bulk/BulkBar';
import { BulkOutcomeHost } from '../bulk/BulkOutcomeHost';
import { KeysButton } from '../commands/KeysButton';
import { KeysDialog } from '../commands/KeysDialog';
import { ListKeysHost } from '../commands/ListKeysHost';
import { OpenCommandHost } from '../commands/OpenCommandHost';
import { ReorderConflict } from '../drag/ReorderConflict';
import type { useReorder } from '../drag/useReorder';
import { BacklogSearchBox } from '../search/BacklogSearchBox';
import { MovingOffBanner } from '../search/MovingOffBanner';
import { useBacklogSearch } from '../search/useBacklogSearch';
import { SelectionProvider } from '../selection/SelectionProvider';
import { BeforeYouPlan } from '../strip/BeforeYouPlan';
import { useCheckQueue } from '../strip/useCheckQueue';
import { BacklogToolbar } from '../toolbar/BacklogToolbar';
import { ForecastToggle } from './ForecastToggle';
import { forecastMarks } from './forecastMarks';
import type { PlanningDrop } from './planningTarget';
import { PlanningDnd } from './PlanningDnd';
import { PlanningSections } from './PlanningSections';
import type { SectionCollapse } from './useSectionCollapse';
import type { BacklogRowDto } from '../../../api/views';

const NO_MARKS: ReturnType<typeof forecastMarks> = new Map();

// The planning screen, inside the commands and bulk providers PlanningMode mounts: the toolbar, the sections with
// their drag and drop, the bulk bar. The check queue narrows the rows first, then the search; either one on turns
// moving off. The selection's walk leaves out the rows of collapsed sections.
export function PlanningScreen({
  project,
  view,
  forecast,
  onForecastChange,
  reorder,
  flashing,
  collapse,
  onMove,
}: {
  project: ProjectDto;
  view: BacklogViewDto;
  forecast: boolean;
  onForecastChange: (on: boolean) => void;
  reorder: ReturnType<typeof useReorder>;
  // Ids of the rows a teammate just changed.
  flashing: ReadonlySet<string>;
  collapse: SectionCollapse;
  onMove: (row: BacklogRowDto, drop: PlanningDrop) => void;
}) {
  const [adding, setAdding] = useState(false);
  const queue = useCheckQueue();
  const search = useBacklogSearch();
  const sections = useMemo(
    () =>
      view.sections.map((section) => ({
        ...section,
        rows: search.apply(queue.apply(section.rows)),
      })),
    [view.sections, search, queue],
  );
  const searchCollapse = useMemo(
    () => collapse.withSearch(search.active, sections),
    [collapse, search.active, sections],
  );
  const walkOrder = useMemo(
    () =>
      sections
        .filter((section) => searchCollapse.isOpen(section.id))
        .flatMap((section) => section.rows.map((row) => row.id)),
    [sections, searchCollapse],
  );
  const filtering = search.active || queue.active;
  return (
    <SelectionProvider walkOrder={walkOrder}>
      <ListKeysHost />
      <OpenCommandHost
        rows={sections.flatMap((section) => section.rows)}
        projectKey={project.key}
      />
      <PasteHost projectId={project.id} projectKey={project.key}>
        <div className="flex min-h-0 flex-1 flex-col">
          <BacklogToolbar
            projectKey={project.key}
            level="stories"
            mode="planning"
            onNewItem={() => setAdding(true)}
            options={() => (
              <>
                <AddManyButton />
                <ForecastToggle checked={forecast} onChange={onForecastChange} />
              </>
            )}
            search={() => <BacklogSearchBox search={search} />}
            keys={() => <KeysButton />}
          />
          <MovingOffBanner search={search} queue={queue} />
          {reorder.conflict ? (
            <ReorderConflict
              conflict={reorder.conflict}
              onUseMine={() => void reorder.useMine()}
              onKeepTheirs={() => void reorder.keepTheirs()}
            />
          ) : null}
          <BeforeYouPlan review={view.review} queue={queue} />
          <PlanningDnd
            sections={sections}
            filtering={filtering}
            velocity={view.velocity}
            onMove={onMove}
          >
            {({ tableRef, dropLine, onKeyMove }) => (
              <PlanningSections
                sections={sections}
                velocity={view.velocity}
                forecast={forecast}
                marks={forecast ? forecastMarks(view.forecast) : NO_MARKS}
                projectId={project.id}
                projectKey={project.key}
                adding={adding}
                onCloseAdd={() => setAdding(false)}
                flashing={flashing}
                collapse={searchCollapse}
                noMatch={
                  search.active && !sections.some((s) => s.rows.length > 0)
                    ? search.query.trim()
                    : null
                }
                filtering={filtering}
                tableRef={tableRef}
                dropLine={dropLine}
                onKeyMove={onKeyMove}
              />
            )}
          </PlanningDnd>
          <BulkBar mode="planning" projectId={project.id} projectKey={project.key} view={view} />
          <BulkOutcomeHost projectId={project.id} />
          <KeysDialog />
        </div>
      </PasteHost>
    </SelectionProvider>
  );
}
