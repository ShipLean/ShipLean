import { useMutation, useQueryClient } from '@tanstack/react-query';
import { ApiRequestError } from '@shiplean/core/api/client';
import type { UserRef } from '@shiplean/core/api/refs';
import type { BoardViewDto, CardDto } from '@shiplean/core/api/views';
import { conflictOf, type WorkItemChanges } from '@shiplean/core/api/workItems';
import { showToast } from '../../../../components/toastStore';
import { patchWorkItem } from '../../../workItem/workItemApi';
import { boardRootKey } from '../../boardApi';

const NO_CHANGES: WorkItemChanges = {
  title: null,
  description: null,
  acceptanceCriteria: null,
  reproSteps: null,
  severity: null,
  state: null,
  assignee: null,
  sprint: null,
  priority: null,
  points: null,
  parent: null,
  tags: null,
  addLinks: [],
  removeLinkIds: [],
};

/** The view with the person (or none) and the revision a write returned on the card. */
export function withAssignee(
  view: BoardViewDto,
  itemId: string,
  assignee: UserRef | null,
  revision: number,
): BoardViewDto {
  return {
    ...view,
    lanes: view.lanes.map((lane) => ({
      ...lane,
      cells: lane.cells.map((cell) => ({
        ...cell,
        cards: cell.cards.map((card) =>
          card.id === itemId ? { ...card, assignee, revision } : card,
        ),
      })),
    })),
  };
}

/**
 * Sets who a card is assigned to through the ordinary work item update, with the card's revision. The board shows
 * the new assignee only once the server accepted it; a refused write (a stale revision says someone else changed the
 * card) leaves the card as it was, says why in a toast and refetches.
 */
export function useAssignCard(projectId: string, boardId: string) {
  const queryClient = useQueryClient();
  const root = boardRootKey(projectId, boardId);
  const { mutate } = useMutation({
    mutationFn: ({ card, person }: { card: CardDto; person: UserRef | null }) =>
      patchWorkItem(card.id, {
        expectedRevision: card.revision,
        changes: { ...NO_CHANGES, assignee: { value: person?.id ?? null } },
      }),
    onSuccess: (result, { card, person }) => {
      queryClient.setQueriesData<BoardViewDto>({ queryKey: root }, (view) =>
        view ? withAssignee(view, card.id, person, result.revision) : view,
      );
    },
    onError: (error, { card }) => {
      showToast(
        conflictOf(error)
          ? `${card.key} was changed by someone else, so it was not reassigned.`
          : `${card.key} could not be reassigned${error instanceof ApiRequestError ? `: ${error.message}` : '.'}`,
        { tone: 'warn' },
      );
    },
    onSettled: () => void queryClient.invalidateQueries({ queryKey: root }),
  });
  return (card: CardDto, person: UserRef | null) => mutate({ card, person });
}
