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
import { CapacityHeader } from './CapacityHeader';
import type { CapacityLine } from './CapacityRow';
import { CapacitySummary } from './CapacitySummary';
import { CapacityTable } from './CapacityTable';
import { CapacityToolbar } from './CapacityToolbar';
import { showToast } from '../../../components/toastStore';
import {
  changedUnderneath,
  draftOf,
  draftTotals,
  layOver,
  parseVelocity,
  roundPoints,
  rowOf,
  rowsDiffer,
  shapeOf,
  tableMembers,
  toPut,
  touchAll,
  touchRow,
  velocityChangedUnderneath,
  velocityTextOf,
  type CapacityDraft,
  type DraftRow,
  type TouchedRows,
  type TouchedVelocity,
} from './capacityDraft';
import { previousSprint } from './previousSprint';

interface CapacityPanelProps {
  project: ProjectDto;
  sprint: SprintDto;
  sprints: SprintDto[];
}

/**
 * The Capacity body. Only the rows the user changed are kept; the table and the save are the server's latest rows with
 * those laid over them, so a teammate's save shows up and is not undone. A row changed on both sides is flagged. The
 * velocity text is kept only once typed in; until then the box shows what the server holds (or the default).
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
  const [velocityTouched, setVelocityTouched] = useState<TouchedVelocity | null>(null);
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

  const stored = capacity.data;
  const shape = shapeOf(stored);
  const ended = stored.ended;
  const server = draftOf(stored);
  const draft = layOver(server, touched);
  const serverVelocityText = velocityTextOf(stored);
  const velocityText = velocityTouched?.text ?? serverVelocityText;
  const typedVelocity = parseVelocity(velocityText);
  const worked = draftTotals(members.data, draft, typedVelocity, shape);
  const inTable = tableMembers(members.data, draft);
  // An old sprint with points but no velocity: until a velocity is typed there is nothing to recompute from, so the
  // stored points and total stand (planning still uses them).
  const pointsOnly =
    !ended && stored.velocity === null && stored.members.length > 0 && typedVelocity === undefined;
  const storedPoints = (userId: string) => stored.members.find((m) => m.user.id === userId)?.points;
  const lines: CapacityLine[] = ended
    ? stored.members.map((m) => ({ user: m.user, row: rowOf(server, m.user.id), points: m.points }))
    : inTable.map((m) => ({
        user: m.user,
        row: rowOf(draft, m.user.id),
        points: pointsOnly ? storedPoints(m.user.id) : worked.points[m.user.id],
      }));
  const entered = ended ? stored.members.length > 0 : worked.entered;
  const pointsOnlyTotal =
    Object.keys(touched).length === 0
      ? stored.teamTotal
      : roundPoints(lines.reduce((sum, line) => sum + (line.points ?? 0), 0));
  const total = ended ? stored.teamTotal : pointsOnly ? pointsOnlyTotal : worked.total;
  const planned = velocity.data.override ?? velocity.data.average;
  const overridden = velocity.data.override !== null;
  const clashes = changedUnderneath(members.data, server, touched);
  const velocityClash = velocityChangedUnderneath(velocityTouched, serverVelocityText);
  const typeVelocity = (text: string) => {
    setVelocityTouched({ text, base: velocityTouched?.base ?? serverVelocityText });
    setProblem(null);
  };
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
    const result = toPut(members.data, draft, velocityText, shape);
    if ('error' in result) {
      setProblem(result.error);
      return;
    }
    save.mutate(result.put, {
      onSuccess: () => {
        setTouched({});
        setVelocityTouched(null);
      },
      onError: (error) =>
        setProblem(error instanceof ApiRequestError ? error.message : 'The save failed.'),
    });
  };
  return (
    <div data-testid={ids.capacity} className="grid gap-3 p-4">
      {ended ? (
        <p
          data-testid={ids.capacityEnded}
          className="m-0 text-[length:var(--text-sm)] text-ink-muted"
        >
          {sprint.name} has ended, so its capacity is read-only.
        </p>
      ) : (
        <CapacityToolbar
          addable={members.data.filter((m) => !inTable.includes(m)).map((m) => m.user)}
          previousName={previous?.name}
          copyBlocked={copyBlocked}
          dirty={rowsDiffer(server, draft) || velocityText !== serverVelocityText}
          saving={save.isPending}
          onAdd={(userId) => editRow(userId, { present: true, days: '', note: '' })}
          onCopy={() => {
            if (!previousCapacity.data || !previous) return;
            editAll(draftOf(previousCapacity.data, shape.workingDays));
            if (previousCapacity.data.velocity !== null) {
              typeVelocity(`${previousCapacity.data.velocity}`);
            }
            showToast(`Copied capacity from ${previous.name}`, { tone: 'ok' });
          }}
          onClear={() => {
            editAll({});
            showToast(`Cleared. ${sprint.name} now plans against velocity`);
          }}
          onSave={onSave}
        />
      )}
      {clashes.length === 0 && !velocityClash ? null : (
        <p data-testid={ids.capacityConflict} role="status" className="m-0 text-caution">
          {clashes.length === 0
            ? 'A teammate changed the velocity while you were editing. Saving replaces it with yours.'
            : `${clashes.map((m) => m.user.name).join(', ')} changed capacity${velocityClash ? ' and the velocity' : ''} while you were editing. Saving replaces their numbers with yours.`}
        </p>
      )}
      {problem === null ? null : (
        <p data-testid={ids.capacityError} role="alert" className="m-0 text-alert">
          {problem}
        </p>
      )}
      <div className="grid items-start gap-4 lg:grid-cols-[minmax(0,1fr)_20rem]">
        <div className="panel min-w-0 overflow-hidden">
          <CapacityHeader
            project={project}
            capacity={stored}
            velocityText={velocityText}
            velocityInvalid={velocityText.trim() !== '' && typedVelocity === undefined}
            readOnly={ended}
            needsVelocity={pointsOnly}
            onVelocity={typeVelocity}
          />
          <CapacityTable
            lines={lines}
            shape={shape}
            total={total}
            readOnly={ended}
            velocity={planned}
            onChange={editRow}
            onRemove={(userId) => editRow(userId, { ...rowOf(draft, userId), present: false })}
          />
        </div>
        <CapacitySummary
          sprintName={sprint.name}
          total={total}
          entered={entered}
          pointsOnly={pointsOnly}
          committed={metrics.data.committed}
          velocity={planned}
          typedVelocity={ended ? (stored.velocity ?? undefined) : typedVelocity}
          overridden={overridden}
          velocityWindow={velocity.data.window}
        />
      </div>
    </div>
  );
}
