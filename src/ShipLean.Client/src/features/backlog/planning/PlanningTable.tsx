import { Fragment, useEffect, useRef, useState } from 'react';
import {
  closestCenter,
  DndContext,
  KeyboardSensor,
  pointerWithin,
  PointerSensor,
  useSensor,
  useSensors,
  type CollisionDetection,
  type DragEndEvent,
  type DragMoveEvent,
  type DragOverEvent,
} from '@dnd-kit/core';
import { sortableKeyboardCoordinates } from '@dnd-kit/sortable';
import type { VelocityDto } from '../../../api/sprints';
import type { BacklogRowDto, BacklogSectionDto } from '../../../api/views';
import { BACKLOG_COLUMNS, type BacklogColumnId } from '../backlogApi';
import { NewItemRow } from '../list/NewItemRow';
import { backlogTestIds as ids } from '../testIds';
import { BacklogSectionHeader } from './BacklogSectionHeader';
import { CreateSprint } from './CreateSprint';
import { forecastMarks } from './forecastMarks';
import {
  dropByKey,
  dropOnRow,
  dropOnSection,
  type DropLine,
  sectionOfDropId,
  type PlanningDrop,
} from './planningTarget';
import { PlanningSectionRows } from './PlanningSectionRows';
import { SectionHeader } from './SectionHeader';
import { useRowFlip } from './useRowFlip';
import { useSprintDates } from './useSprintDates';

const COLUMNS: readonly BacklogColumnId[] = ['rank', 'key', 'title', 'parent', 'state', 'points'];
const numeric: readonly BacklogColumnId[] = ['rank', 'points'];

// A pointer over a row or header drops there; with the keyboard (no pointer) the nearest one does.
const collisions: CollisionDetection = (args) => {
  const within = pointerWithin(args);
  return within.length > 0 ? within : closestCenter(args);
};

// Whether a row was put down on the lower half of the row `over`. With a pointer: where the pointer is against the
// row's place in the table (offsets, which sliding rows and a scrolled page leave alone). From the keyboard: whether
// the row came from above, so moving down puts it after the row it lands on, as in the list.
function droppedBelow(
  table: HTMLTableElement | null,
  active: DragEndEvent['active'],
  over: NonNullable<DragEndEvent['over']>,
  pointerY: number | null,
): boolean {
  const row = table?.querySelector<HTMLElement>(`[data-row-id="${String(over.id)}"]`);
  if (table && row && pointerY !== null) {
    const inTable = pointerY - table.getBoundingClientRect().top - table.clientTop;
    return inTable > row.offsetTop + row.offsetHeight / 2;
  }
  const from = active.rect.current.initial;
  return from !== null && from.top < over.rect.top;
}

// The sprint sections (all expanded) stacked above the Backlog, with "+ Create sprint" between them. The rows keep
// the list's cells; a forecast cut line is drawn under the last item that fits each forecast sprint. A row is dragged
// by its grip onto another row (just above it, or just below when dropped on its lower half) or onto a section header.
export function PlanningTable({
  sections,
  velocity,
  forecast,
  marks,
  projectId,
  projectKey,
  adding,
  onCloseAdd,
  flashing,
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
  // Ids of the rows a teammate just changed.
  flashing: ReadonlySet<string>;
  onMove: (row: BacklogRowDto, drop: PlanningDrop) => void;
}) {
  const columnCount = COLUMNS.length + 1 + (forecast ? 1 : 0);
  const sprints = sections.filter((s) => s.sprint !== null);
  const backlog = sections.find((s) => s.sprint === null);
  const planned = velocity === null ? null : (velocity.override ?? velocity.average);
  const dates = useSprintDates(projectId);
  const tableRef = useRef<HTMLTableElement>(null);
  const refocus = useRef<string | null>(null);
  // Where the pointer is now, in the page.
  const pointerY = useRef<number | null>(null);
  useEffect(() => {
    // Captured, so it is up to date before dnd-kit's own listeners read it.
    const track = (event: PointerEvent) => {
      pointerY.current = event.clientY;
    };
    window.addEventListener('pointermove', track, true);
    return () => window.removeEventListener('pointermove', track, true);
  }, []);
  useRowFlip(tableRef);
  useEffect(() => {
    if (refocus.current === null) return;
    const row = tableRef.current?.querySelector<HTMLElement>(`[data-row-id="${refocus.current}"]`);
    if (row) {
      row.focus();
      refocus.current = null;
    }
  });
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 4 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  );
  const rows = sections.flatMap((section) => section.rows);
  // The row the dragged one would land beside, and on which edge: the line the design draws where the row will land.
  const [line, setLine] = useState<DropLine | null>(null);
  // dnd-kit reports a new `over` through onDragOver; onDragMove's own `over` can be one render behind the pointer. So
  // the target is kept from onDragOver and the edge is worked out again on every move.
  const overNow = useRef<DragOverEvent['over']>(null);
  function drawLine({ active, activatorEvent }: Pick<DragMoveEvent, 'active' | 'activatorEvent'>) {
    const over = overNow.current;
    const next =
      over === null || active.id === over.id || sectionOfDropId(String(over.id)) !== undefined
        ? null
        : {
            id: String(over.id),
            below: droppedBelow(
              tableRef.current,
              active,
              over,
              activatorEvent instanceof MouseEvent ? pointerY.current : null,
            ),
          };
    setLine((current) =>
      current?.id === next?.id && current?.below === next?.below ? current : next,
    );
  }
  function onDragOver(event: DragOverEvent) {
    overNow.current = event.over;
    drawLine(event);
  }
  function onDragEnd({ active, over, activatorEvent }: DragEndEvent) {
    setLine(null);
    overNow.current = null;
    const row = rows.find((r) => r.id === active.id);
    if (over === null || row === undefined) return;
    const overId = String(over.id);
    let drop: PlanningDrop | null;
    const overSection = sectionOfDropId(overId);
    if (overSection !== undefined) {
      drop = dropOnSection(sections, row.id, overSection);
    } else {
      drop = dropOnRow(
        sections,
        row.id,
        overId,
        droppedBelow(
          tableRef.current,
          active,
          over,
          activatorEvent instanceof MouseEvent ? pointerY.current : null,
        ),
      );
    }
    if (drop) onMove(row, drop);
  }
  function onKeyMove(row: BacklogRowDto, direction: 'up' | 'down') {
    const drop = dropByKey(sections, row.id, direction);
    if (drop === null) return;
    refocus.current = row.id;
    onMove(row, drop);
  }
  const body = (section: BacklogSectionDto) => (
    <PlanningSectionRows
      section={section}
      columns={COLUMNS}
      columnCount={columnCount}
      projectKey={projectKey}
      marks={marks}
      forecast={forecast}
      flashing={flashing}
      dropLine={line}
      onKeyMove={onKeyMove}
    />
  );
  return (
    <div data-testid={ids.planning} className="flex-1 overflow-auto">
      <DndContext
        sensors={sensors}
        collisionDetection={collisions}
        onDragOver={onDragOver}
        onDragMove={drawLine}
        onDragEnd={onDragEnd}
        onDragCancel={() => {
          setLine(null);
          overNow.current = null;
        }}
      >
        <table ref={tableRef} className="w-full border-collapse">
          <thead>
            <tr className="h-8 border-b border-edge text-left text-[length:var(--text-xs)] text-ink-muted">
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
                />
                {body(section)}
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
                />
                {adding ? (
                  <NewItemRow
                    projectId={projectId}
                    projectKey={projectKey}
                    columnCount={columnCount}
                    onClose={onCloseAdd}
                  />
                ) : null}
                {body(backlog)}
              </>
            ) : null}
          </tbody>
        </table>
      </DndContext>
    </div>
  );
}
