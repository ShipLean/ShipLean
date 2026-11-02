import { useEffect, useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import type { WorkItemDto } from '../../../api/workItems';
import type { BacklogRowDto } from '../../../api/views';
import { showToast } from '../../../components/toastStore';
import { registerInvalidation } from '../../../realtime';
import { BURST_WINDOW_MS, burstText, isBurst } from './teammateBurst';
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
    type Pending = () => void;
    let pending: Pending[] = [];
    let pendingIds = new Set<string>();
    let windowTimer: ReturnType<typeof setTimeout> | undefined;
    let quietTimer: ReturnType<typeof setTimeout> | undefined;
    let burstIds: Set<string> | undefined;
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
      if (sprintMoves.has(row.id)) return;
      showToast(`${row.key} moved in the backlog`);
    }
    async function announce(row: BacklogRowDto, what: (key: string, item?: WorkItemDto) => string) {
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
    function endBurstWhenQuiet() {
      clearTimeout(quietTimer);
      quietTimer = setTimeout(() => {
        quietTimer = undefined;
        const ids = burstIds;
        burstIds = undefined;
        if (ids !== undefined) showToast(burstText(ids.size));
      }, BURST_WINDOW_MS);
    }
    // The window has closed: more than three rows changed in it is a burst (one toast once it goes quiet, no item
    // reads); otherwise each change is announced, in the order it arrived.
    function classify() {
      windowTimer = undefined;
      const batch = pending;
      const ids = pendingIds;
      pending = [];
      pendingIds = new Set();
      if (!isBurst(ids.size)) {
        batch.forEach((run) => run());
        return;
      }
      burstIds = new Set(ids);
      endBurstWhenQuiet();
    }
    // Every row event flashes its row now; what the toast says waits until the window closes, to see if it is a burst.
    function onRow(id: string, announceIt: (row: BacklogRowDto) => void) {
      const row = rowOf(id);
      if (row === undefined) return;
      flash(row.id);
      if (burstIds !== undefined) {
        burstIds.add(row.id);
        endBurstWhenQuiet();
        return;
      }
      pending.push(() => announceIt(row));
      pendingIds.add(row.id);
      windowTimer ??= setTimeout(classify, BURST_WINDOW_MS);
    }
    const rowOf = (id: string) => rowsRef.current.find((r) => r.id === id);
    const unregister = [
      registerInvalidation('workItemUpdated', (event) =>
        onRow(event.id, (row) => {
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
      ),
      registerInvalidation('workItemMoved', (event) =>
        onRow(event.id, (row) => void announce(row, (key) => `moved ${key}`)),
      ),
      registerInvalidation('workItemReordered', (event) => onRow(event.id, announceReorder)),
    ];
    return () => {
      unregister.forEach((off) => off());
      live.forEach(clearTimeout);
      live.clear();
      sprintMoves.clear();
      clearTimeout(windowTimer);
      clearTimeout(quietTimer);
    };
  }, [projectId, queryClient]);

  return flashing;
}
