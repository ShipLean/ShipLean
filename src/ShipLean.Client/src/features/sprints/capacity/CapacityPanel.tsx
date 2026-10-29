import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { ApiRequestError } from '../../../api/client';
import type { ProjectDto } from '../../../api/projects';
import type { SprintDto } from '../../../api/sprints';
import { fetchMembers, projectMembersKey } from '../../../queries/shared';
import {
  fetchSprintCapacity,
  sprintCapacityKey,
  useCapacityRealtime,
  useSaveSprintCapacity,
  useSprintCapacity,
  useSprintMetrics,
  useVelocity,
} from '../sprintsApi';
import { sprintsTestIds as ids } from '../testIds';
import { CapacitySummary } from './CapacitySummary';
import { CapacityTable } from './CapacityTable';
import { CapacityToolbar } from './CapacityToolbar';
import { showToast } from '../../../components/toastStore';
import {
  changedUnderneath,
  draftOf,
  draftTotal,
  layOver,
  toPut,
  touchAll,
  touchRow,
  type CapacityDraft,
  type DraftRow,
  type TouchedRows,
} from './capacityDraft';
import { previousSprint } from './previousSprint';

interface CapacityPanelProps {
  project: ProjectDto;
  sprint: SprintDto;
  sprints: SprintDto[];
}

/**
 * The Capacity body. Only the rows the user changed are kept; the table and the save are the server's latest rows with
 * those laid over them, so a teammate's save shows up and is not undone. A row changed on both sides is flagged.
 */
export function CapacityPanel({ project, sprint, sprints }: CapacityPanelProps) {
  useCapacityRealtime(project.id);
  const members = useQuery({
    queryKey: projectMembersKey(project.id),
    queryFn: () => fetchMembers(project.id),
  });
  const capacity = useSprintCapacity(project.id, sprint.id);
  const metrics = useSprintMetrics(project.id, sprint.id);
  const velocity = useVelocity(project.id);
  const previous = previousSprint(sprints, sprint);
  const previousCapacity = useQuery({
    queryKey: sprintCapacityKey(project.id, previous?.id ?? ''),
    queryFn: () => fetchSprintCapacity(previous?.id ?? ''),
    enabled: previous !== undefined,
  });
  const save = useSaveSprintCapacity(project.id, sprint.id);
  const [touched, setTouched] = useState<TouchedRows>({});
  const [problem, setProblem] = useState<string | null>(null);

  const failed = members.isError || capacity.isError || metrics.isError || velocity.isError;
  if (failed) {
    return (
      <p data-testid={ids.capacityLoadError} role="alert" className="m-0 p-4 text-alert">
        The capacity could not be loaded.
      </p>
    );
  }
  if (!members.data || !capacity.data || !metrics.data || !velocity.data) {
    return <p className="m-0 p-4 text-ink-muted">Loading…</p>;
  }

  const server = draftOf(capacity.data);
  const draft = layOver(server, touched);
  const { total, entered } = draftTotal(members.data, draft);
  const planned = velocity.data.override ?? velocity.data.average;
  const overridden = velocity.data.override !== null;
  const clashes = changedUnderneath(members.data, server, touched);
  const editRow = (userId: string, row: DraftRow) => {
    setTouched(touchRow(touched, server, userId, row));
    setProblem(null);
  };
  const editAll = (next: CapacityDraft) => {
    setTouched(touchAll(touched, server, members.data, next));
    setProblem(null);
  };
  const copyFailed = previousCapacity.isError;
  const copyBlocked = copyFailed
    ? 'The capacity of the sprint before could not be loaded'
    : (previousCapacity.data?.members.length ?? 0) === 0
      ? 'The sprint before has no capacity entered'
      : undefined;
  const onSave = () => {
    const result = toPut(members.data, draft);
    if ('error' in result) {
      setProblem(result.error);
      return;
    }
    save.mutate(
      { velocity: null, members: result.put },
      {
        onSuccess: () => setTouched({}),
        onError: (error) =>
          setProblem(error instanceof ApiRequestError ? error.message : 'The save failed.'),
      },
    );
  };
  return (
    <div data-testid={ids.capacity}>
      <CapacityToolbar
        previousName={previous?.name}
        copyBlocked={copyBlocked}
        dirty={Object.keys(touched).length > 0}
        saving={save.isPending}
        onCopy={() => {
          if (!previousCapacity.data || !previous) return;
          editAll(draftOf(previousCapacity.data));
          showToast(`Copied capacity from ${previous.name}`, { tone: 'ok' });
        }}
        onClear={() => {
          editAll({});
          showToast(`Cleared. ${sprint.name} now plans against velocity`);
        }}
        onSave={onSave}
      />
      {clashes.length === 0 ? null : (
        <p data-testid={ids.capacityConflict} role="status" className="m-0 px-4 pt-2 text-caution">
          {`${clashes.map((m) => m.user.name).join(', ')} changed capacity while you were editing. Saving replaces ${
            clashes.length === 1 ? 'their number' : 'their numbers'
          } with yours.`}
        </p>
      )}
      {problem === null ? null : (
        <p data-testid={ids.capacityError} role="alert" className="m-0 px-4 pt-2 text-alert">
          {problem}
        </p>
      )}
      <div className="m-4 flex flex-col rounded-panel border border-edge bg-panel lg:flex-row">
        <div className="min-w-0 flex-1">
          <CapacityTable
            members={members.data}
            draft={draft}
            total={total}
            entered={entered}
            velocity={planned}
            onChange={editRow}
          />
        </div>
        <CapacitySummary
          sprintName={sprint.name}
          total={total}
          entered={entered}
          committed={metrics.data.committed}
          velocity={planned}
          overridden={overridden}
          velocityWindow={velocity.data.window}
        />
      </div>
    </div>
  );
}
