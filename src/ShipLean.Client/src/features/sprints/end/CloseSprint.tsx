import { useState } from 'react';
import type { ProjectDto } from '@shiplean/core/api/projects';
import type { RolloverDto, SprintDto } from '@shiplean/core/api/sprints';
import { Dialog } from '../../../components/Dialog';
import { showToast } from '../../../components/toastStore';
import {
  useApplyRollover,
  useRollover,
  useSprintEndRealtime,
  useSprintMetrics,
} from '../sprintsApi';
import { sprintsTestIds as ids } from '../testIds';
import { canCloseSprint } from './canCloseSprint';
import { CloseSprintConfirm } from './CloseSprintConfirm';
import { defaultChoices, rolloverMoves, type Choices } from './rolloverChoices';
import { SprintEndDialog } from './SprintEndDialog';
import { SprintEndDone } from './SprintEndDone';

/**
 * The Close sprint button. Closing a sprint is something a person chooses to do: nothing opens by itself. The dialog
 * stays open if a teammate closes the sprint meanwhile, and says so. Given `open` and `onClose`, the dialog is opened
 * from elsewhere (the touch sprint header's ⋯) and the button is left out.
 */
export function CloseSprint({
  project,
  sprint,
  open: openFromOutside,
  onClose,
}: {
  project: ProjectDto;
  sprint: SprintDto;
  open?: boolean;
  onClose?: () => void;
}) {
  useSprintEndRealtime(project.id);
  const [openHere, setOpenHere] = useState(false);
  const controlled = openFromOutside !== undefined;
  const open = controlled ? openFromOutside : openHere;
  const rollover = useRollover(project.id, open ? sprint.id : undefined);
  const close = () => {
    if (controlled) onClose?.();
    else setOpenHere(false);
  };
  return (
    <>
      {!controlled && canCloseSprint(sprint) ? (
        <button
          type="button"
          data-testid={ids.closeSprint}
          className="btn-ghost"
          onClick={() => setOpenHere(true)}
        >
          Close sprint
        </button>
      ) : null}
      {open && rollover.data ? (
        <CloseSprintDialog project={project} rollover={rollover.data} onDone={close} />
      ) : null}
      {open && rollover.isError ? (
        <Dialog title={`Close ${sprint.name}`} onClose={close}>
          <p data-testid={ids.endError} role="alert" className="m-0 text-alert">
            The sprint could not be loaded. Try again.
          </p>
        </Dialog>
      ) : null}
    </>
  );
}

function CloseSprintDialog({
  project,
  rollover,
  onDone,
}: {
  project: ProjectDto;
  rollover: RolloverDto;
  onDone: () => void;
}) {
  const sprintId = rollover.sprint.id;
  const metrics = useSprintMetrics(project.id, sprintId);
  const [choices, setChoices] = useState<Choices | null>(null);
  const [error, setError] = useState<string | null>(null);
  const apply = useApplyRollover(project.id, sprintId, onDone);
  const current = choices ?? defaultChoices(rollover.items);

  if (rollover.status === 'decided') {
    return <SprintEndDone sprintName={rollover.sprint.name} onClose={onDone} />;
  }

  const onClose = async () => {
    setError(null);
    try {
      const moves = rolloverMoves(rollover.items, current, rollover.nextSprint?.id ?? null);
      await apply.mutateAsync({ moves, endToday: rollover.endsEarly });
      const moved = moves.length;
      showToast(
        moved === 0
          ? `Closed ${rollover.sprint.name}`
          : `Closed ${rollover.sprint.name} and moved ${moved} ${moved === 1 ? 'item' : 'items'} out`,
        { tone: 'ok' },
      );
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'The sprint could not be closed.');
    }
  };

  if (rollover.items.length === 0) {
    return (
      <CloseSprintConfirm
        sprintName={rollover.sprint.name}
        endsEarly={rollover.endsEarly}
        onCancel={onDone}
        onClose={() => void onClose()}
        closing={apply.isPending}
        error={error}
      />
    );
  }
  return (
    <SprintEndDialog
      rollover={rollover}
      metrics={metrics.data}
      choices={current}
      onChoices={setChoices}
      onApply={() => void onClose()}
      onCancel={onDone}
      applying={apply.isPending}
      error={error}
    />
  );
}
