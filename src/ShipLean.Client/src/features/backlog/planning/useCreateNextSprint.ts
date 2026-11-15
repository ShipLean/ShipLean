import { useQueryClient } from '@tanstack/react-query';
import { showToast } from '../../../components/toastStore';
import { useCreateSprint, useNextSprint } from '../../sprints/sprintsApi';
import { backlogRootKey } from '../backlogApi';

/** Creates the sprint the server proposes (the next number, the last sprint's length); the new section then appears. */
export function useCreateNextSprint(projectId: string) {
  const queryClient = useQueryClient();
  const next = useNextSprint(projectId);
  const create = useCreateSprint(projectId);
  return {
    disabled: next.data === undefined || create.isPending,
    create: () => {
      if (next.data === undefined) return;
      void create
        .mutateAsync(next.data)
        .then(() => queryClient.invalidateQueries({ queryKey: backlogRootKey(projectId) }))
        .catch((error: unknown) =>
          showToast(error instanceof Error ? error.message : 'Could not create the sprint', {
            tone: 'danger',
          }),
        );
    },
  };
}
