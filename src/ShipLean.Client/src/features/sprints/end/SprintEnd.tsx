import { useState } from 'react';
import type { ProjectDto } from '../../../api/projects';
import type { SprintDto } from '../../../api/sprints';
import { showToast } from '../../../components/toastStore';
import {
  useApplyRollover,
  useRollover,
  useSprintEndRealtime,
  useSprintMetrics,
} from '../sprintsApi';
import { defaultChoices, rolloverMoves, type Choices } from './rolloverChoices';
import { SprintEndBanner } from './SprintEndBanner';
import { SprintEndDialog } from './SprintEndDialog';
import { SprintEndDone } from './SprintEndDone';
import { sprintToWrapUp } from './sprintEndTarget';
import { useDecideLater } from './useDecideLater';

/**
 * The Sprint end prompt: once a sprint has ended and nobody has decided what happens to its unfinished stories and
 * bugs, the dialog asks. It stays on the sprint it first showed, so a teammate's apply shows the done state instead of
 * the next ended sprint.
 */
export function SprintEnd({ project, sprints }: { project: ProjectDto; sprints: SprintDto[] }) {
  const candidate = sprintToWrapUp(sprints);
  const [seen, setSeen] = useState<string | undefined>(candidate?.id);
  if (candidate !== undefined && candidate.id !== seen) setSeen(candidate.id);
  const sprintId = candidate?.id ?? seen;
  if (sprintId === undefined) return null;
  return <WrapUp key={sprintId} project={project} sprintId={sprintId} />;
}

function WrapUp({ project, sprintId }: { project: ProjectDto; sprintId: string }) {
  useSprintEndRealtime(project.id);
  const rollover = useRollover(project.id, sprintId);
  const metrics = useSprintMetrics(project.id, sprintId);
  const [later, setLater] = useDecideLater(sprintId);
  const [choices, setChoices] = useState<Choices | null>(null);
  const [prompted, setPrompted] = useState(false);
  const [closed, setClosed] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const apply = useApplyRollover(project.id, sprintId, () => setClosed(true));
  const data = rollover.data;
  if (!data || closed) return null;
  if (data.status === 'decided') {
    return prompted && !later ? (
      <SprintEndDone sprintName={data.sprint.name} onClose={() => setClosed(true)} />
    ) : null;
  }
  if (data.items.length === 0) return null;
  if (later) {
    return <SprintEndBanner rollover={data} onReview={() => setLater(false)} />;
  }
  if (!prompted) setPrompted(true);

  const current = choices ?? defaultChoices(data.items);
  const onApply = async () => {
    setError(null);
    try {
      const moves = rolloverMoves(data.items, current, data.nextSprint?.id ?? null);
      const moved = data.items.filter((item) =>
        moves.some((m) => m.workItemId === item.workItemId),
      );
      await apply.mutateAsync({ moves });
      showToast(
        `Moved ${moved.length} ${moved.length === 1 ? 'item' : 'items'} out of ${data.sprint.name}`,
        { tone: 'ok' },
      );
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'The sprint could not be wrapped up.');
    }
  };
  return (
    <SprintEndDialog
      rollover={data}
      metrics={metrics.data}
      choices={current}
      onChoices={setChoices}
      onApply={() => void onApply()}
      onLater={() => setLater(true)}
      applying={apply.isPending}
      error={error}
    />
  );
}
