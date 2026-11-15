import { backlogTestIds as ids } from '../testIds';
import { useCreateNextSprint } from './useCreateNextSprint';

// "+ Create sprint": creates the sprint the server proposes (the next number, the last sprint's length) and the new
// section appears.
export function CreateSprint({
  projectId,
  touch = false,
}: {
  projectId: string;
  // At 900px and below the button is a full touch target.
  touch?: boolean;
}) {
  const sprint = useCreateNextSprint(projectId);
  return (
    <button
      type="button"
      data-testid={ids.planningCreateSprint}
      className={touch ? 'btn-ghost min-h-[var(--touch-target)]' : 'btn-ghost'}
      disabled={sprint.disabled}
      onClick={sprint.create}
    >
      + Create sprint
    </button>
  );
}
