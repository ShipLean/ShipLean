import { useMemo, useState } from 'react';
import type { SheetAction } from '../../../touch/ActionSheet';
import { useTouchShell } from '../../../touch/useTouchShell';
import type { ProjectDto } from '@shiplean/core/api/projects';
import type { BacklogViewDto } from '@shiplean/core/api/views';
import { AddBelowProvider } from '../add/AddBelowProvider';
import { AddManyAction, AddManyButton } from '../add/AddManyButton';
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
import { QueueFixedProvider } from '../strip/QueueFixedProvider';
import { BeforeYouPlan } from '../strip/BeforeYouPlan';
import { useCheckQueue } from '../strip/useCheckQueue';
import { SelectBar } from '../touch/SelectBar';
import { useSelectMoreActions } from '../touch/selectMoreActions';
import { BacklogToolbar } from '../toolbar/BacklogToolbar';
import { createSprintIcon, forecastIcon, velocityIcon } from '../toolbar/moreIcons';
import { ForecastToggle } from './ForecastToggle';
import { forecastMarks } from './forecastMarks';
import type { PlanningDrop } from './planningTarget';
import { PlanningDnd } from './PlanningDnd';
import { PlanningSections } from './PlanningSections';
import { PlanningTouch } from './touch/PlanningTouch';
import { TouchVelocitySheet } from './touch/TouchVelocitySheet';
import { useCreateNextSprint } from './useCreateNextSprint';
import type { SectionCollapse } from './useSectionCollapse';
import type { BacklogRowDto } from '@shiplean/core/api/views';

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
  // At 900px and below Planning is the touch layout, and Override velocity and Create sprint are in ⋯ More.
  const { touch } = useTouchShell();
  const [overriding, setOverriding] = useState(false);
  const createSprint = useCreateNextSprint(project.id);
  const planned = view.velocity === null ? null : (view.velocity.override ?? view.velocity.average);
  const moreActions: SheetAction[] = [
    // A switch: tapping it keeps the sheet open (K1 toggle).
    {
      id: 'forecast',
      label: 'Forecast',
      icon: forecastIcon,
      hint: 'Marks where each backlog item is likely to land',
      group: '',
      toggle: { on: forecast },
      onSelect: () => onForecastChange(!forecast),
    },
    ...(forecast && planned !== null
      ? [
          {
            id: 'override-velocity',
            label: 'Override velocity',
            icon: velocityIcon,
            detail: planned,
            onSelect: () => setOverriding(true),
          } satisfies SheetAction,
        ]
      : []),
    {
      id: 'create-sprint',
      label: 'Create sprint',
      icon: createSprintIcon,
      disabled: createSprint.disabled,
      onSelect: createSprint.create,
    },
  ];
  const queue = useCheckQueue();
  const selectActions = useSelectMoreActions(queue, view.review);
  const search = useBacklogSearch();
  const queuedSections = useMemo(
    () =>
      touch
        ? view.sections
        : view.sections.map((section) => ({ ...section, rows: queue.apply(section.rows) })),
    [touch, view.sections, queue],
  );
  const queued = useMemo(() => queuedSections.flatMap((section) => section.rows), [queuedSections]);
  const sections = useMemo(
    () => queuedSections.map((section) => ({ ...section, rows: search.apply(section.rows) })),
    [queuedSections, search],
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
  // Drag and the pull line stop for a search or a check queue; adding stops for a search only. At touch the queue lives
  // in the Estimate sheet and the rows behind it stay whole (SHIP-1626 BACKLOG-3), so it narrows nothing.
  const queueNarrows = queue.active && !touch;
  const filtering = search.active || queueNarrows;
  return (
    <QueueFixedProvider review={view.review} queue={queue} queued={queued}>
      <SelectionProvider walkOrder={walkOrder}>
        <ListKeysHost />
        <OpenCommandHost
          rows={sections.flatMap((section) => section.rows)}
          projectKey={project.key}
        />
        <AddBelowProvider projectId={project.id} projectKey={project.key}>
          <PasteHost projectId={project.id} projectKey={project.key}>
            <div className="flex min-h-0 flex-1 flex-col">
              <AddManyAction>
                {(addMany) => (
                  <BacklogToolbar
                    projectKey={project.key}
                    level="stories"
                    mode="planning"
                    onNewItem={() => setAdding(true)}
                    options={
                      touch
                        ? undefined
                        : () => (
                            <>
                              <AddManyButton />
                              <ForecastToggle checked={forecast} onChange={onForecastChange} />
                            </>
                          )
                    }
                    search={() => <BacklogSearchBox search={search} />}
                    keys={() => <KeysButton />}
                    moreActions={[...(addMany ? [addMany] : []), ...selectActions, ...moreActions]}
                  />
                )}
              </AddManyAction>
              <MovingOffBanner search={search} queueNarrows={queueNarrows} />
              {reorder.conflict ? (
                <ReorderConflict
                  conflict={reorder.conflict}
                  onUseMine={() => void reorder.useMine()}
                  onKeepTheirs={() => void reorder.keepTheirs()}
                />
              ) : null}
              <div className="sticky top-0 z-[5] flex-none">
                <BeforeYouPlan review={view.review} queue={queue} />
              </div>
              <BulkOutcomeHost projectId={project.id} />
              <SelectBar />
              {touch ? (
                <PlanningTouch
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
                  addingOff={search.active}
                  onMove={onMove}
                />
              ) : (
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
                      addingOff={search.active}
                      tableRef={tableRef}
                      dropLine={dropLine}
                      onKeyMove={onKeyMove}
                    />
                  )}
                </PlanningDnd>
              )}
              {touch && view.velocity !== null ? (
                <TouchVelocitySheet
                  open={overriding}
                  onClose={() => setOverriding(false)}
                  velocity={view.velocity}
                  projectId={project.id}
                  projectKey={project.key}
                />
              ) : null}
              <BulkBar
                mode="planning"
                projectId={project.id}
                projectKey={project.key}
                view={view}
              />
              <KeysDialog />
            </div>
          </PasteHost>
        </AddBelowProvider>
      </SelectionProvider>
    </QueueFixedProvider>
  );
}
