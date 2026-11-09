import { useContext, useEffect, useRef, useState, useSyncExternalStore } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import type { BacklogRowDto } from '@shiplean/core/api/views';
import { conflictOf } from '@shiplean/core/api/workItems';
import { showToast } from '../../../components/toastStore';
import { patchWorkItem } from '../../workItem/workItemApi';
import { changesOf } from '../bulk/changesOf';
import { backlogRootKey } from '../backlogApi';
import { useListCommand } from '../commands/listCommands';
import { cachedRow } from './cachedRow';
import { SelectionContext, useSelection } from '../selection/useSelection';

// Which row's points field is open, and the digit that opened it. One field is open at a time on a screen, so the
// state lives here and every points cell reads it.
interface Editing {
  id: string;
  seed: string | null;
}
let editing: Editing | null = null;
const listeners = new Set<() => void>();
function setEditing(next: Editing | null) {
  editing = next;
  listeners.forEach((listener) => listener());
}
function subscribe(listener: () => void) {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

const MAX_POINTS = 9999.99;

/** The points a field's text means: empty clears, a number from 0 to 9999.99 with up to two decimals sets; anything else is invalid. */
export function parsePoints(text: string): { ok: true; value: number | null } | { ok: false } {
  const trimmed = text.trim();
  if (trimmed === '') return { ok: true, value: null };
  if (!/^\d+(\.\d{1,2})?$/.test(trimmed)) return { ok: false };
  const value = Number(trimmed);
  return value <= MAX_POINTS ? { ok: true, value } : { ok: false };
}

/** A save the server answered 409 to: the typed value stays, with who changed the row and the revision it is at now. */
export interface EstimateConflict {
  value: number | null;
  changedBy: string;
  currentRevision: number;
}

export interface Estimate {
  /** False outside the selection provider (Tree mode): the points are read-only. */
  editable: boolean;
  isEditing: boolean;
  /** The digit that opened the field, if one did. */
  seed: string | null;
  /** The points to show: a value being saved or in conflict shows until the backlog is read again. */
  shown: number | null;
  conflict: EstimateConflict | null;
  open: () => void;
  /** Closes the field with nothing sent. */
  cancel: () => void;
  /** Saves `value` (when it changed) and closes the field; `walk` opens the next row's field, if there is one. */
  commit: (value: number | null, walk: boolean) => void;
  useMine: () => void;
  keepTheirs: () => void;
}

/** The estimate of one row: the open field, the save (a normal edit at the row's revision, not K1) and its conflict. Registers the `estimate` command. */
export function useEstimate(row: BacklogRowDto): Estimate {
  const queryClient = useQueryClient();
  const selection = useSelection();
  const editable = useContext(SelectionContext) !== null;
  const current = useSyncExternalStore(subscribe, () => editing);
  const [pending, setPending] = useState<{ value: number | null } | null>(null);
  const [conflict, setConflict] = useState<EstimateConflict | null>(null);
  // What our own last save wrote and the revision it made.
  const written = useRef<{ value: number | null; revision: number } | null>(null);
  const inFlight = useRef(false);
  const queued = useRef<{ value: number | null } | null>(null);

  useListCommand('estimate', (arg) => {
    if (selection.focusedId === null) return;
    setEditing({
      id: selection.focusedId,
      seed: typeof arg === 'string' && /^\d$/.test(arg) ? arg : null,
    });
  });

  const isEditing = current?.id === row.id;
  useEffect(
    () => () => {
      if (editing?.id === row.id) setEditing(null);
    },
    [row.id],
  );

  // The row as the backlog holds it now. The rendered row is all there is only when no backlog read holds it.
  const latest = () => cachedRow(queryClient, row.id);
  const refetch = (projectId: string | undefined) =>
    projectId === undefined
      ? Promise.resolve()
      : queryClient.invalidateQueries({ queryKey: backlogRootKey(projectId) });

  // One save at a time per row: an edit made while one is pending waits in `queued` and goes out the moment that save
  // is answered, from the answer's value and revision, so it never depends on the backlog being read again.
  // `explicitRevision` is the conflict's, for Use mine.
  async function send(value: number | null, explicitRevision?: number) {
    const cached = latest();
    const base = cached?.row ?? row;
    // What the row holds now: our own last write, until a backlog read at or after its revision shows it.
    const own = written.current;
    const stored = own !== null && own.revision > base.revision ? own.value : base.points;
    if (explicitRevision === undefined && value === stored) {
      if (!inFlight.current) setPending(null);
      return;
    }
    const revision = explicitRevision ?? Math.max(base.revision, own?.revision ?? 0);
    inFlight.current = true;
    setPending({ value });
    let stale: EstimateConflict | null = null;
    try {
      const result = await patchWorkItem(row.id, {
        expectedRevision: revision,
        changes: changesOf({ points: { value } }),
      });
      written.current = { value, revision: result.revision };
      setConflict(null);
    } catch (error) {
      const conflicted = conflictOf(error);
      if (conflicted) {
        stale = {
          value,
          changedBy:
            conflicted.theirChanges[0]?.changedBy?.name ??
            conflicted.current.changedBy?.name ??
            'Someone',
          currentRevision: conflicted.current.revision,
        };
      } else {
        showToast(error instanceof Error ? error.message : `Could not estimate ${row.key}`, {
          tone: 'danger',
        });
      }
    }
    const next = queued.current;
    queued.current = null;
    if (next !== null && stale !== null) {
      // The typed value replaces the conflicting one; Use mine sends it at the revision the conflict named.
      stale = { ...stale, value: next.value };
    }
    if (stale !== null) setConflict(stale);
    const projectId = latest()?.projectId ?? cached?.projectId;
    if (next !== null && stale === null) {
      // Straight away, whether or not the backlog read below works, or this cell is still on screen.
      inFlight.current = false;
      void refetch(projectId);
      await send(next.value);
      return;
    }
    inFlight.current = false;
    await refetch(projectId);
    if (!inFlight.current) setPending(null);
  }

  function commit(value: number | null, walk: boolean) {
    const at = selection.orderedIds.indexOf(row.id);
    const next = walk && at >= 0 ? selection.orderedIds[at + 1] : undefined;
    setEditing(next === undefined ? null : { id: next, seed: null });
    if (next !== undefined) selection.setFocused(next);
    if (inFlight.current) {
      queued.current = { value };
      setPending({ value });
      return;
    }
    void send(value);
  }

  return {
    editable,
    isEditing,
    seed: isEditing ? current.seed : null,
    shown: conflict ? conflict.value : pending ? pending.value : row.points,
    conflict,
    open: () => {
      selection.setFocused(row.id);
      setEditing({ id: row.id, seed: null });
    },
    cancel: () => setEditing(null),
    commit,
    useMine: () => {
      if (conflict !== null) void send(conflict.value, conflict.currentRevision);
    },
    keepTheirs: () => {
      setConflict(null);
      void refetch(latest()?.projectId);
    },
  };
}
