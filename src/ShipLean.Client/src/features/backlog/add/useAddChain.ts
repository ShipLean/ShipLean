import { useRef, useState, useSyncExternalStore } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { ApiRequestError } from '../../../api/client';
import type { BacklogAddRequest } from '../../../api/backlogWrites';
import type { WorkItemTypeId } from '../../../api/refs';
import type { BacklogViewDto } from '../../../api/views';
import type { ParentRefDto } from '../../../api/workItems';
import { backlogRootKey } from '../../../queries/backlogKeys';
import { useBacklogAdd } from '../bulk/useBacklogAdd';
import { useSelection } from '../selection/useSelection';
import { defaultPlace, placeContext, placeRequest } from './pastePlace';

/** What one typed row holds. Points are null when left empty. */
export interface AddFields {
  type: WorkItemTypeId;
  title: string;
  points: number | null;
  parent: ParentRefDto | null;
}

/** The open add-below row: under which row, and the type and parent that stay until it closes. */
export interface BelowState {
  anchorId: string;
  type: WorkItemTypeId;
  parent: ParentRefDto | null;
}

// One add-below row is open at a time, and it moves to the row just made, which is another component instance:
// the state it carries lives here, not in the row.
let below: BelowState | null = null;
const listeners = new Set<() => void>();
const setBelow = (next: BelowState | null) => {
  below = next;
  listeners.forEach((listener) => listener());
};
const subscribe = (listener: () => void) => {
  listeners.add(listener);
  return () => void listeners.delete(listener);
};

export const useBelowState = (): BelowState | null => useSyncExternalStore(subscribe, () => below);

/** Opens the row under `anchorId`; one already open keeps its type and parent. */
export const openBelow = (anchorId: string) =>
  setBelow({ type: 'story', parent: null, ...below, anchorId });
export const closeBelow = () => setBelow(null);
export const stickBelow = (change: Partial<Pick<BelowState, 'type' | 'parent'>>) => {
  if (below !== null) setBelow({ ...below, ...change });
};

/** The backlog view read last, to place a row by the order and sprints it shows; opening the row asks for nothing. */
function newestView(
  queryClient: ReturnType<typeof useQueryClient>,
  projectId: string,
): BacklogViewDto | undefined {
  const newest = queryClient
    .getQueryCache()
    .findAll({ queryKey: backlogRootKey(projectId) })
    .filter((query) => query.state.data !== undefined)
    .sort((a, b) => b.state.dataUpdatedAt - a.state.dataUpdatedAt)[0];
  return newest?.state.data as BacklogViewDto | undefined;
}

/**
 * Creates the typed rows one at a time (K2). `below` puts a row just under `anchorId`, as the paste review does, and
 * returns the new row's id so the caller makes it the next anchor: a typed list keeps its order. `inSprint` puts a row
 * last in a sprint. Both return null when nothing was created (a write already in flight, or a failed one: `error`).
 */
export function useAddChain(projectId: string, projectKey: string) {
  const add = useBacklogAdd(projectId, projectKey);
  const queryClient = useQueryClient();
  const selection = useSelection();
  const busy = useRef(false);
  const [error, setError] = useState<string | null>(null);

  async function send(
    fields: AddFields,
    where: Pick<BacklogAddRequest, 'sprintId' | 'placement'>,
  ): Promise<string | null> {
    if (busy.current) return null;
    busy.current = true;
    setError(null);
    try {
      const result = await add.mutateAsync({
        items: [
          {
            type: fields.type,
            title: fields.title,
            description: null,
            points: fields.points,
            parentId: fields.parent?.id ?? null,
            tasks: [],
          },
        ],
        ...where,
      });
      return result.created[0]?.id ?? null;
    } catch (failure) {
      setError(failure instanceof ApiRequestError ? failure.message : 'Could not add the item.');
      return null;
    } finally {
      busy.current = false;
    }
  }

  return {
    error,
    clearError: () => setError(null),
    below: (anchorId: string, fields: AddFields) => {
      const context = placeContext(
        newestView(queryClient, projectId),
        anchorId,
        selection.orderedIds,
      );
      return send(fields, placeRequest(defaultPlace(context), context));
    },
    inSprint: (sprintId: string | null, fields: AddFields) =>
      send(fields, { sprintId, placement: { slot: 'last', beforeItemId: null } }),
  };
}
