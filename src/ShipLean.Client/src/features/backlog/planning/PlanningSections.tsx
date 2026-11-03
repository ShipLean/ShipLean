import { Fragment, useMemo, type RefObject } from 'react';
import type { VelocityDto } from '../../../api/sprints';
import type { BacklogRowDto, BacklogSectionDto } from '../../../api/views';
import { AddInButton, AddInSection } from '../add/AddInSection';
import { BACKLOG_COLUMNS, type BacklogColumnId } from '../backlogApi';
import { NewItemRow } from '../list/NewItemRow';
import { SelectAllCell } from '../selection/SelectAllCell';
import { useSelection } from '../selection/useSelection';
import { backlogTestIds as ids } from '../testIds';
import { BacklogSectionHeader } from './BacklogSectionHeader';
import { CreateSprint } from './CreateSprint';
import type { forecastMarks } from './forecastMarks';
import type { DropLine } from './planningTarget';
import { PlanningBacklogRows } from './PlanningBacklogRows';
import { PlanningSectionRows } from './PlanningSectionRows';
import { SectionHeader } from './SectionHeader';
import type { SectionCollapse } from './useSectionCollapse';
import { useSprintDates } from './useSprintDates';

const COLUMNS: readonly BacklogColumnId[] = ['rank', 'key', 'title', 'parent', 'state', 'points'];
const numeric: readonly BacklogColumnId[] = ['rank', 'points'];

// The sprint sections stacked above the Backlog, with "+ Create sprint" between them. The rows keep the list's cells;
// a forecast cut line is drawn under the last item that fits each forecast sprint. Dragging is PlanningDnd's.
export function PlanningSections({
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
  noMatch = null,
  filtering,
  addingOff,
  tableRef,
  dropLine,
  onKeyMove,
}: {
  sections: BacklogSectionDto[];
  velocity: VelocityDto | null;
  forecast: boolean;
  marks: ReturnType<typeof forecastMarks>;
  projectId: string;
  projectKey: string;
  adding: boolean;
  onCloseAdd: () => void;
  // Ids of the rows a teammate just changed.
  flashing: ReadonlySet<string>;
  collapse: SectionCollapse;
  // What the search typed, when it left no row in any section; null otherwise.
  noMatch?: string | null;
  // A filter, search or check queue is on: moving is off, and rank is relative to rows you can't see.
  filtering: boolean;
  // A filter or search is on: adding is off, because rank is relative to rows you can't see. A check queue leaves it on.
  addingOff: boolean;
  tableRef: RefObject<HTMLTableElement | null>;
  dropLine: DropLine | null;
  onKeyMove: (row: BacklogRowDto, direction: 'up' | 'down') => void;
}) {
  // The select column, the grip column and the data columns, and the forecast column when it shows.
  const columnCount = COLUMNS.length + 2 + (forecast ? 1 : 0);
  const sprints = sections.filter((s) => s.sprint !== null);
  const backlog = sections.find((s) => s.sprint === null);
  const planned = velocity === null ? null : (velocity.override ?? velocity.average);
  const dates = useSprintDates(projectId);
  const shownIds = useMemo(
    () => sections.flatMap((section) => section.rows.map((row) => row.id)),
    [sections],
  );
  useSelection(shownIds);
  const sprintBody = (section: BacklogSectionDto) =>
    collapse.isOpen(section.id) ? (
      <>
        <PlanningSectionRows
          section={section}
          columns={COLUMNS}
          columnCount={columnCount}
          projectId={projectId}
          projectKey={projectKey}
          marks={marks}
          forecast={forecast}
          flashing={flashing}
          dropLine={dropLine}
          filtering={filtering}
          addingOff={addingOff}
          onKeyMove={onKeyMove}
        />
        <AddInSection section={section} columnCount={columnCount} projectId={projectId} />
      </>
    ) : null;
  return (
    <div data-testid={ids.planning} className="flex-1 overflow-auto">
      <table ref={tableRef} className="w-full border-collapse">
        <thead>
          <tr className="h-8 border-b border-edge text-left text-[length:var(--text-xs)] text-ink-muted">
            <SelectAllCell />
            {forecast ? (
              <th data-testid={ids.planningForecastLabel} className="w-[92px] px-2 font-semibold">
                Forecast
              </th>
            ) : null}
            <th className="w-6" />
            {COLUMNS.map((column) => (
              <th
                key={column}
                className={`px-2 font-semibold ${numeric.includes(column) ? 'text-right' : ''}`}
              >
                {BACKLOG_COLUMNS.find((c) => c.id === column)?.label}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {sprints.map((section) => (
            <Fragment key={section.id}>
              <SectionHeader
                section={section}
                projectKey={projectKey}
                velocity={planned}
                datesLabel={dates.get(section.id)}
                columnCount={columnCount}
                collapse={collapse}
                action={<AddInButton section={section} filtering={addingOff} collapse={collapse} />}
              />
              {sprintBody(section)}
            </Fragment>
          ))}
          <tr className="h-10">
            <td colSpan={columnCount} className="px-2">
              <CreateSprint projectId={projectId} />
            </td>
          </tr>
          {backlog ? (
            <>
              <BacklogSectionHeader
                section={backlog}
                velocity={velocity}
                forecast={forecast}
                projectId={projectId}
                projectKey={projectKey}
                columnCount={columnCount}
                collapse={collapse}
              />
              {adding ? (
                <NewItemRow
                  projectId={projectId}
                  projectKey={projectKey}
                  columnCount={columnCount}
                  onClose={onCloseAdd}
                />
              ) : null}
              {collapse.isOpen(backlog.id) ? (
                <PlanningBacklogRows
                  section={backlog}
                  sprints={sprints}
                  columns={COLUMNS}
                  columnCount={columnCount}
                  projectId={projectId}
                  projectKey={projectKey}
                  marks={marks}
                  forecast={forecast}
                  velocity={velocity}
                  flashing={flashing}
                  dropLine={dropLine}
                  filtering={filtering}
                  addingOff={addingOff}
                  onKeyMove={onKeyMove}
                />
              ) : null}
            </>
          ) : null}
          {noMatch === null ? null : (
            <tr>
              <td
                colSpan={columnCount}
                data-testid={ids.noRows}
                className="h-16 text-center text-[length:var(--text-sm)] text-ink-muted"
              >
                Nothing matches &quot;{noMatch}&quot;
              </td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  );
}
