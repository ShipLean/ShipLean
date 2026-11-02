import { useContext, useEffect, useRef, useState, useSyncExternalStore } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import type { BacklogRowDto } from '../../../api/views';
import { conflictOf } from '../../../api/workItems';
import { showToast } from '../../../components/toastStore';
import { patchWorkItem } from '../../workItem/workItemApi';
import { changesOf } from '../bulk/changesOf';
import { useListCommand } from '../commands/listCommands';
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
  // The revision our own last save made, until the backlog is read again with it.
  const savedRevision = useRef(0);

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

  const refetch = () =>
    queryClient.invalidateQueries({
      predicate: (query) => query.queryKey[0] === 'projects' && query.queryKey[2] === 'backlog',
    });

  async function write(value: number | null, revision: number) {
    setPending({ value });
    try {
      const result = await patchWorkItem(row.id, {
        expectedRevision: revision,
        changes: changesOf({ points: { value } }),
      });
      savedRevision.current = result.revision;
      setConflict(null);
    } catch (error) {
      const stale = conflictOf(error);
      if (stale) {
        setConflict({
          value,
          changedBy:
            stale.theirChanges[0]?.changedBy?.name ?? stale.current.changedBy?.name ?? 'Someone',
          currentRevision: stale.current.revision,
        });
      } else {
        showToast(error instanceof Error ? error.message : `Could not estimate ${row.key}`, {
          tone: 'danger',
        });
      }
    }
    await refetch();
    setPending(null);
  }

  function commit(value: number | null, walk: boolean) {
    const at = selection.orderedIds.indexOf(row.id);
    const next = walk && at >= 0 ? selection.orderedIds[at + 1] : undefined;
    setEditing(next === undefined ? null : { id: next, seed: null });
    if (next !== undefined) selection.setFocused(next);
    if (value !== row.points) void write(value, Math.max(row.revision, savedRevision.current));
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
      if (conflict !== null) void write(conflict.value, conflict.currentRevision);
    },
    keepTheirs: () => {
      setConflict(null);
      void refetch();
    },
  };
}
