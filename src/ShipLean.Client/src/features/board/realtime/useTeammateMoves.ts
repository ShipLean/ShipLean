import { useEffect, useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import type { BoardViewDto } from '../../../api/views';
import { showToast } from '../../../components/toastStore';
import { registerInvalidation } from '../../../realtime';
import { fetchWorkItem, workItemKey } from '../../workItem/workItemApi';
import { locate } from '../moves/applyMove';

const FLASH_MS = 2500;

/**
 * A teammate's move of a card on the board: the card flashes (it slides in when the board refetches) and a toast
 * names who moved it and where. The events are remote only (a tab ignores its own). A move that only reordered the
 * card within its cell names nobody: the item's last editor is not who reordered it. Returns the ids of the cards
 * flashing now.
 */
export function useTeammateMoves(
  projectId: string,
  view: BoardViewDto | undefined,
): ReadonlySet<string> {
  const queryClient = useQueryClient();
  const [flashing, setFlashing] = useState<ReadonlySet<string>>(new Set());
  const shown = useRef(view);
  const timers = useRef(new Set<ReturnType<typeof setTimeout>>());
  useEffect(() => {
    shown.current = view;
  }, [view]);

  useEffect(() => {
    const live = timers.current;
    function flash(id: string) {
      setFlashing((current) => new Set(current).add(id));
      const timer = setTimeout(() => {
        live.delete(timer);
        setFlashing((current) => {
          const next = new Set(current);
          next.delete(id);
          return next;
        });
      }, FLASH_MS);
      live.add(timer);
    }
    const unregister = registerInvalidation('workItemMoved', (event) => {
      const at = shown.current && locate(shown.current, event.id);
      if (!at) return;
      flash(event.id);
      const samePlace =
        event.columnId === at.columnId &&
        event.done === at.done &&
        (event.laneId === null || event.laneId === at.laneId);
      if (samePlace) {
        showToast(`${at.card.key} was reordered`);
        return;
      }
      void queryClient
        .fetchQuery({
          queryKey: workItemKey(projectId, at.card.key),
          queryFn: () => fetchWorkItem(projectId, at.card.key),
          staleTime: 0,
        })
        // The item can't be read (gone, or offline): the move still happened, so the toast says so without a name.
        .catch(() => undefined)
        .then((item) => {
          showToast(
            `${item?.changedBy?.name ?? 'A teammate'} moved ${at.card.key} to ${item?.state.name ?? 'another column'}`,
          );
        });
    });
    return () => {
      unregister();
      live.forEach(clearTimeout);
      live.clear();
    };
  }, [projectId, queryClient]);

  return flashing;
}
