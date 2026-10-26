import type { SprintGoalEditSlotProps } from '../../../slots/SprintGoalEditSlot';
import { SprintGoal } from '../SprintGoal';
import { sprintHeaderHarnessIds as harness } from './harnessIds';

function FinishingEditor({ onDone }: SprintGoalEditSlotProps) {
  return (
    <button type="button" data-testid={harness.editorDone} onClick={onDone}>
      Done
    </button>
  );
}

export function SprintGoalHarness(props: Omit<Parameters<typeof SprintGoal>[0], 'Editor'>) {
  return <SprintGoal {...props} Editor={FinishingEditor} />;
}
