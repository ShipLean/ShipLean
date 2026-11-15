import { useMemo } from 'react';
import type { VelocityDto } from '@shiplean/core/api/sprints';
import type { BacklogRowDto, BacklogSectionDto } from '@shiplean/core/api/views';
import { AddInSection } from '../../add/AddInSection';
import { NewItemRow } from '../../list/NewItemRow';
import { usePullLine, type PullLine } from '../../pull/usePullLine';
import { useSelection } from '../../selection/useSelection';
import { backlogTestIds as ids } from '../../testIds';
import { CreateSprint } from '../CreateSprint';
import type { forecastMarks } from '../forecastMarks';
import { sectionDropId, type PlanningDrop } from '../planningTarget';
import type { SectionCollapse } from '../useSectionCollapse';
import { useSprintDates } from '../useSprintDates';
import { forecastLabels } from './forecastLabels';
import { TouchBacklogBlock } from './TouchBacklogBlock';
import { TouchPlanningRow } from './TouchPlanningRow';
import { TouchPullLine } from './TouchPullLine';
import { TouchSprintBlock } from './TouchSprintBlock';
import { useTouchPlanningDrop, type TouchPlanningDrop } from './useTouchPlanningDrop';

function SectionRows({
  section,
  projectKey,
  marks,
  forecast,
  flashing,
  filtering,
  drag,
  pull,
}: {
  section: BacklogSectionDto;
  projectKey: string;
  marks: ReadonlyMap<string, string>;
  forecast: boolean;
  flashing: ReadonlySet<string>;
  filtering: boolean;
  drag: TouchPlanningDrop;
  // The pull line, drawn under the row where the cut falls (the Backlog only).
  pull?: PullLine;
}) {
  const labels = useMemo(
    () => (forecast ? forecastLabels(section.rows, marks) : new Map<string, string>()),
    [forecast, section.rows, marks],
  );
  const first = section.rows[0];
  return (
    <div data-testid={ids.planningTouchRows} data-section={section.id} data-pull-body="">
      {pull?.atTop && first ? <TouchPullLine line={pull} /> : null}
      {section.rows.map((row: BacklogRowDto) => (
        <div key={row.id}>
          <TouchPlanningRow
            row={row}
            projectKey={projectKey}
            sectionId={section.id}
            forecast={labels.get(row.id)}
            flash={flashing.has(row.id)}
            handle={!filtering}
            onOver={drag.over}
            onDrop={drag.drop}
          />
          {pull?.afterRowId === row.id ? <TouchPullLine line={pull} /> : null}
        </div>
      ))}
    </div>
  );
}

// The Backlog's rows with the pull line, which needs its own hook.
function BacklogRows(props: {
  section: BacklogSectionDto;
  sprints: readonly BacklogSectionDto[];
  velocity: VelocityDto | null;
  projectId: string;
  projectKey: string;
  marks: ReadonlyMap<string, string>;
  forecast: boolean;
  flashing: ReadonlySet<string>;
  filtering: boolean;
  drag: TouchPlanningDrop;
}) {
  const pull = usePullLine({
    section: props.section,
    sprints: props.sprints,
    velocity: props.velocity,
    forecast: props.forecast,
    projectId: props.projectId,
    filtering: props.filtering,
  });
  return <SectionRows {...props} pull={pull} />;
}

/**
 * Planning at 900px and below, in place of PlanningDnd and PlanningSections (so dnd-kit is not mounted): each sprint a
 * header block over its touch rows, "+ Create sprint", then the Backlog with the pull line. A row lifted by its handle
 * and let go over a header or another row moves through `onMove`, the path the desktop drag uses.
 */
export function PlanningTouch({
  sections,
  velocity,
  forecast,
  marks,
  projectId,
  projectKey,
  adding,
  onCloseAdd,
  flashing,
  collapse,
  noMatch,
  filtering,
  addingOff,
  onMove,
}: {
  sections: BacklogSectionDto[];
  velocity: VelocityDto | null;
  forecast: boolean;
  marks: ReturnType<typeof forecastMarks>;
  projectId: string;
  projectKey: string;
  adding: boolean;
  onCloseAdd: () => void;
  flashing: ReadonlySet<string>;
  collapse: SectionCollapse;
  // What the search typed, when it left no row in any section; null otherwise.
  noMatch: string | null;
  // A filter, search or check queue is on: moving is off.
  filtering: boolean;
  // A filter or search is on: adding is off.
  addingOff: boolean;
  onMove: (row: BacklogRowDto, drop: PlanningDrop) => void;
}) {
  const drag = useTouchPlanningDrop({ sections, velocity, onMove });
  const sprints = sections.filter((s) => s.sprint !== null);
  const backlog = sections.find((s) => s.sprint === null);
  const planned = velocity === null ? null : (velocity.override ?? velocity.average);
  const dates = useSprintDates(projectId);
  const shownIds = useMemo(
    () => sections.flatMap((section) => section.rows.map((row) => row.id)),
    [sections],
  );
  useSelection(shownIds);
  const litOf = (section: BacklogSectionDto) =>
    drag.lit?.sectionId === section.id ? drag.lit : null;
  const rowProps = { projectKey, marks, forecast, flashing, filtering, drag };
  return (
    <div data-testid={ids.planning} className="min-w-0 flex-1">
      <div data-testid={ids.planningTouch}>
        {sprints.map((section) => (
          <div key={section.id}>
            <TouchSprintBlock
              section={section}
              projectKey={projectKey}
              velocity={planned}
              datesLabel={dates.get(section.id)}
              collapse={collapse}
              addingOff={addingOff}
              lit={litOf(section)}
            />
            {collapse.isOpen(section.id) ? (
              <>
                {section.rows.length === 0 ? (
                  <div
                    data-testid={ids.planningEmptySection}
                    data-drop-target={sectionDropId(section.id, 'hint')}
                    data-over={drag.lit?.sectionId === section.id}
                    className="flex min-h-[52px] items-center justify-center text-center text-[length:var(--text-sm)] text-ink-muted"
                  >
                    No stories or bugs planned in {section.title}
                  </div>
                ) : (
                  <SectionRows section={section} {...rowProps} />
                )}
                <table className="w-full">
                  <tbody>
                    <AddInSection section={section} columnCount={1} projectId={projectId} />
                  </tbody>
                </table>
              </>
            ) : null}
          </div>
        ))}
        <div className="px-2 py-1">
          <CreateSprint projectId={projectId} touch />
        </div>
        {backlog ? (
          <>
            <TouchBacklogBlock
              section={backlog}
              velocity={velocity}
              forecast={forecast}
              collapse={collapse}
              lit={litOf(backlog)}
            />
            {adding ? (
              <NewItemRow
                projectId={projectId}
                projectKey={projectKey}
                columnCount={1}
                onClose={onCloseAdd}
              />
            ) : null}
            {collapse.isOpen(backlog.id) ? (
              <BacklogRows
                section={backlog}
                sprints={sprints}
                velocity={velocity}
                projectId={projectId}
                {...rowProps}
              />
            ) : null}
          </>
        ) : null}
        {noMatch === null ? null : (
          <div
            data-testid={ids.noRows}
            className="flex h-16 items-center justify-center text-[length:var(--text-sm)] text-ink-muted"
          >
            Nothing matches &quot;{noMatch}&quot;
          </div>
        )}
      </div>
    </div>
  );
}
