import { useEffect, useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import type { WorkItemDto } from '../../../api/workItems';
import type { BacklogRowDto } from '../../../api/views';
import { showToast } from '../../../components/toastStore';
import { registerInvalidation } from '../../../realtime';
import { fetchWorkItem, workItemKey } from '../../workItem/workItemApi';

const FLASH_MS = 2500;
// A sprint move is a sprint change then a reorder: the reorder that follows within this long adds no toast of its own.
const MOVE_MS = 3000;

// "storyPoints" -> "story points".
const words = (field: string) => field.replace(/([A-Z])/g, ' $1').toLowerCase();

/**
 * A teammate's change to an item in the list: the row flashes and a toast names who changed what. The events are remote
 * only (a tab ignores its own); the backlog itself is refetched by useBacklog. Returns the ids of the rows flashing now.
 */
export function useTeammateChanges(
  projectId: string,
  rows: readonly BacklogRowDto[],
): ReadonlySet<string> {
  const queryClient = useQueryClient();
  const [flashing, setFlashing] = useState<ReadonlySet<string>>(new Set());
  const rowsRef = useRef(rows);
  const timers = useRef(new Set<ReturnType<typeof setTimeout>>());
  const moving = useRef(new Set<string>());
  useEffect(() => {
    rowsRef.current = rows;
  }, [rows]);

  useEffect(() => {
    const live = timers.current;
    const sprintMoves = moving.current;
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
    // A reorder changes neither the item's revision nor its changedBy, and its event carries no actor, so nobody is named.
    function announceReorder(row: BacklogRowDto) {
      flash(row.id);
      if (sprintMoves.has(row.id)) return;
      showToast(`${row.key} moved in the backlog`);
    }
    async function announce(row: BacklogRowDto, what: (key: string, item?: WorkItemDto) => string) {
      flash(row.id);
      const item = await queryClient
        .fetchQuery({
          queryKey: workItemKey(projectId, row.key),
          queryFn: () => fetchWorkItem(projectId, row.key),
          staleTime: 0,
        })
        // The item can't be read (gone, or offline): the change still happened, so the toast says so without a name.
        .catch(() => undefined);
      showToast(`${item?.changedBy?.name ?? 'A teammate'} ${what(row.key, item)}`);
    }
    const rowOf = (id: string) => rowsRef.current.find((r) => r.id === id);
    const unregister = [
      registerInvalidation('workItemUpdated', (event) => {
        const row = rowOf(event.id);
        if (row === undefined) return;
        if (event.fields.includes('sprint')) {
          sprintMoves.add(row.id);
          const timer = setTimeout(() => {
            live.delete(timer);
            sprintMoves.delete(row.id);
          }, MOVE_MS);
          live.add(timer);
          void announce(
            row,
            (key, item) => `moved ${key} to ${item?.sprint?.name ?? 'the Backlog'}`,
          );
          return;
        }
        void announce(row, (key) => `changed ${event.fields.map(words).join(', ')} on ${key}`);
      }),
      registerInvalidation('workItemMoved', (event) => {
        const row = rowOf(event.id);
        if (row) void announce(row, (key) => `moved ${key}`);
      }),
      registerInvalidation('workItemReordered', (event) => {
        const row = rowOf(event.id);
        if (row) announceReorder(row);
      }),
    ];
    return () => {
      unregister.forEach((off) => off());
      live.forEach(clearTimeout);
      live.clear();
      sprintMoves.clear();
    };
  }, [projectId, queryClient]);

  return flashing;
}
