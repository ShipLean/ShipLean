import { useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import type { ProjectDto } from '../../../api/projects';
import type { BacklogRowDto, BacklogViewDto } from '../../../api/views';
import { BACKLOG_COLUMNS, backlogKey, useBacklog, useBacklogView } from '../backlogApi';
import { moveRowToSection, useReorder } from '../drag/useReorder';
import { ReorderConflict } from '../drag/ReorderConflict';
import { useTeammateChanges } from '../realtime/useTeammateChanges';
import { BacklogToolbar } from '../toolbar/BacklogToolbar';
import { backlogTestIds as ids } from '../testIds';
import { ForecastToggle } from './ForecastToggle';
import { forecastMarks } from './forecastMarks';
import type { PlanningDrop } from './planningTarget';
import { PlanningTable } from './PlanningTable';
import { sprintMoveWrite } from './sprintMoveWrite';
import { withWritten } from './withWritten';

// The Stories backlog in planning mode: the sprint sections above the Backlog, capacity bars, forecast lines and the
// velocity override. Planning has no Completed toggle and no Columns menu. A dragged row sets its sprint and its place
// in one action; a stale write offers Use mine / Keep theirs.
export function PlanningMode({ project }: { project: ProjectDto }) {
  const queryClient = useQueryClient();
  const [adding, setAdding] = useState(false);
  const view = useBacklogView('stories', BACKLOG_COLUMNS);
  const backlog = useBacklog({
    projectId: project.id,
    level: 'stories',
    mode: 'planning',
    filter: '',
    includeCompleted: false,
  });
  const queryKey = backlogKey(project.id, 'stories', 'planning', '', false);
  const reorder = useReorder(project.id, queryKey);
  const flashing = useTeammateChanges(
    project.id,
    backlog.data?.sections.flatMap((section) => section.rows) ?? [],
  );
  // Moves are written one at a time: each is sent against the revision the write before it left the item at.
  const sending = useRef<Promise<unknown>>(Promise.resolve());

  function move(row: BacklogRowDto, drop: PlanningDrop) {
    const place = (current: BacklogViewDto) =>
      moveRowToSection(current, row.id, drop.sectionId, drop.placeBefore);
    queryClient.setQueryData<BacklogViewDto>(queryKey, (current) =>
      current === undefined ? current : place(current),
    );
    const run = () => {
      const latest =
        queryClient
          .getQueryData<BacklogViewDto>(queryKey)
          ?.sections.flatMap((section) => section.rows)
          .find((r) => r.id === row.id) ?? row;
      // The PATCH bumps the revision: its answer goes into the cache at once, so the next move sends the revision the
      // server holds even if the backlog has not been read again.
      const sprintWrite = sprintMoveWrite(
        row.id,
        drop.sprintChanged ? (drop.sprint?.id ?? null) : undefined,
        drop.beforeItemId,
        (answer) =>
          queryClient.setQueryData<BacklogViewDto>(queryKey, (current) =>
            current === undefined
              ? current
              : withWritten(current, row.id, answer.revision, drop.sprint),
          ),
        drop.notSaved,
      );
      // The next move waits for this write, not for the backlog to be read again afterwards.
      return new Promise<void>((resolve) => {
        const write = (revision: number) => sprintWrite(revision).finally(resolve);
        void reorder.drop(latest, drop.placeBefore, write, drop.done, place);
      });
    };
    sending.current = sending.current.then(run, run);
  }

  if (backlog.isError || view.failed) {
    return (
      <section data-testid={ids.loadFailed} className="p-6">
        <h1 className="page-title">Could not load the backlog</h1>
        <p className="text-ink-muted">Reload the page to try again.</p>
      </section>
    );
  }
  if (!backlog.isSuccess || !view.loaded) {
    return (
      <div data-testid={ids.loading} className="p-4 text-ink-muted">
        Loading the backlog
      </div>
    );
  }
  const forecast = view.forecast;
  return (
    <div className="flex min-h-0 flex-1 flex-col">
      <BacklogToolbar
        projectKey={project.key}
        level="stories"
        mode="planning"
        onNewItem={() => setAdding(true)}
        options={() => (
          <ForecastToggle checked={forecast} onChange={(on) => view.update({ forecast: on })} />
        )}
      />
      {reorder.conflict ? (
        <ReorderConflict
          conflict={reorder.conflict}
          onUseMine={() => void reorder.useMine()}
          onKeepTheirs={() => void reorder.keepTheirs()}
        />
      ) : null}
      <PlanningTable
        sections={backlog.data.sections}
        velocity={backlog.data.velocity}
        forecast={forecast}
        marks={forecast ? forecastMarks(backlog.data.forecast) : new Map()}
        projectId={project.id}
        projectKey={project.key}
        adding={adding}
        onCloseAdd={() => setAdding(false)}
        flashing={flashing}
        onMove={move}
      />
    </div>
  );
}
