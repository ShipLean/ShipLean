import { useQueryClient } from '@tanstack/react-query';
import { showToast } from '../../../components/toastStore';
import { useCreateSprint, useNextSprint } from '../../sprints/sprintsApi';
import { backlogRootKey } from '../backlogApi';
import { backlogTestIds as ids } from '../testIds';

// "+ Create sprint": creates the sprint the server proposes (the next number, the last sprint's length) and the new
// section appears.
export function CreateSprint({ projectId }: { projectId: string }) {
  const queryClient = useQueryClient();
  const next = useNextSprint(projectId);
  const create = useCreateSprint(projectId);
  return (
    <button
      type="button"
      data-testid={ids.planningCreateSprint}
      className="btn-ghost"
      disabled={next.data === undefined || create.isPending}
      onClick={() => {
        if (next.data === undefined) return;
        void create
          .mutateAsync(next.data)
          .then(() => queryClient.invalidateQueries({ queryKey: backlogRootKey(projectId) }))
          .catch((error: unknown) =>
            showToast(error instanceof Error ? error.message : 'Could not create the sprint', {
              tone: 'danger',
            }),
          );
      }}
    >
      + Create sprint
    </button>
  );
}
