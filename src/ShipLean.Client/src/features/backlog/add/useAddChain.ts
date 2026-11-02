import { createContext, useContext, useRef, useSyncExternalStore } from 'react';
import { ApiRequestError } from '../../../api/client';
import type {
  BacklogAddRequest,
  BacklogAddResult,
  BulkPlacement,
} from '../../../api/backlogWrites';
import type { WorkItemTypeId } from '../../../api/refs';
import type { BacklogViewDto } from '../../../api/views';
import type { ParentRefDto } from '../../../api/workItems';
import { showToast } from '../../../components/toastStore';
import { defaultPlace, placeContext, placeRequest } from './pastePlace';

/** What one typed row holds. Points are null when left empty. */
export interface AddFields {
  type: WorkItemTypeId;
  title: string;
  points: number | null;
  parent: ParentRefDto | null;
}

/** The text in the add row's title and points boxes. */
export interface Draft {
  title: string;
  points: string;
}
export const EMPTY_DRAFT: Draft = { title: '', points: '' };

/** The open add-below row: under which row, the type and parent that stay until it closes, and what is typed. */
export interface BelowState {
  anchorId: string;
  type: WorkItemTypeId;
  parent: ParentRefDto | null;
  draft: Draft;
}

interface Snapshot {
  below: BelowState | null;
  /** The sprint section whose "+ Add" row is open. */
  section: string | null;
  error: string | null;
}

type Where = Pick<BacklogAddRequest, 'sprintId' | 'placement'>;

/** What the provider hands the store on every render: the write, the newest backlog read and focus. */
export interface AddStoreInputs {
  add: (request: BacklogAddRequest) => Promise<BacklogAddResult>;
  view: () => BacklogViewDto | undefined;
  focus: (id: string) => void;
}

/**
 * The state of the add rows of one backlog screen of one project. One add-below row is open at a time and it moves to
 * the row just made, which is another component instance, so what it carries lives here and not in the row. The
 * writes are queued here too: one at a time, each placed by the row the one before made.
 */
export function createAddStore() {
  let snapshot: Snapshot = { below: null, section: null, error: null };
  let tail: string | null = null;
  let queue: Promise<unknown> = Promise.resolve();
  let inputs: AddStoreInputs | null = null;
  const listeners = new Set<() => void>();

  const set = (next: Snapshot) => {
    snapshot = next;
    listeners.forEach((listener) => listener());
  };

  /** Below `anchorId`: before the row that follows it, or last when it is the final row. */
  function belowRequest(anchorId: string): Where {
    const current = inputs;
    if (current === null) throw new Error('The add store has no inputs bound.');
    const view = current.view();
    // The order the view shows, not the selection's: that one catches up a render after the row made is read.
    const context = placeContext(view, anchorId, view?.rows.map((row) => row.id) ?? []);
    return placeRequest(defaultPlace(context), context);
  }

  // Resolves true when the row was made. Never rejects: a failure shows the error and a toast and resolves false.
  function submit(
    fields: AddFields,
    where: () => Where,
    created: (id: string) => void,
  ): Promise<boolean> {
    const run = queue.then(async () => {
      const current = inputs;
      if (current === null) throw new Error('The add store has no inputs bound.');
      set({ ...snapshot, error: null });
      try {
        const result = await current.add({
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
          ...where(),
        });
        created(result.created[0].id);
        return true;
      } catch (failure) {
        const message =
          failure instanceof ApiRequestError ? failure.message : 'Could not add the item.';
        set({ ...snapshot, error: message });
        showToast(`Could not add "${fields.title}": ${message}`, { tone: 'danger' });
        return false;
      }
    });
    queue = run;
    return run;
  }

  return {
    subscribe: (listener: () => void) => {
      listeners.add(listener);
      return () => void listeners.delete(listener);
    },
    snapshot: () => snapshot,
    bind: (next: AddStoreInputs) => {
      inputs = next;
    },
    /** Opens the row under `anchorId` with a story type, no parent and nothing typed. */
    open: (anchorId: string) => {
      tail = anchorId;
      set({
        ...snapshot,
        below: { anchorId, type: 'story', parent: null, draft: EMPTY_DRAFT },
        error: null,
      });
    },
    close: () => set({ ...snapshot, below: null, error: null }),
    openSection: (sectionId: string) => set({ ...snapshot, section: sectionId, error: null }),
    closeSection: () => set({ ...snapshot, section: null, error: null }),
    stick: (change: Partial<Pick<BelowState, 'type' | 'parent'>>) => {
      if (snapshot.below !== null) set({ ...snapshot, below: { ...snapshot.below, ...change } });
    },
    draft: (update: (draft: Draft) => Draft) => {
      if (snapshot.below !== null)
        set({ ...snapshot, below: { ...snapshot.below, draft: update(snapshot.below.draft) } });
    },
    clearError: () => set({ ...snapshot, error: null }),
    /** Creates a row just below the last one made, or below the anchor for the first. */
    addBelow: (fields: AddFields) =>
      submit(
        fields,
        () => belowRequest(tail ?? ''),
        (id) => {
          tail = id;
          const open = snapshot.below;
          if (open === null) return;
          // The open row moves under the row just made; the text typed meanwhile moves with it.
          set({ ...snapshot, below: { ...open, anchorId: id } });
          inputs?.focus(id);
        },
      ),
    /** Creates a row at the top of a sprint for the first, then each one below the row just made. */
    addInSprint: (
      sprintId: string | null,
      after: string | null,
      fields: AddFields,
      done: (id: string) => void,
    ) =>
      submit(
        fields,
        () =>
          after === null
            ? { sprintId, placement: { slot: 'first', beforeItemId: null } satisfies BulkPlacement }
            : belowRequest(after),
        done,
      ),
  };
}

export type AddStore = ReturnType<typeof createAddStore>;

export const AddStoreContext = createContext<AddStore | null>(null);

function useStore(): AddStore {
  const store = useContext(AddStoreContext);
  if (store === null) throw new Error('Add rows need an AddBelowProvider above them.');
  return store;
}

export function useBelowState(): BelowState | null {
  const store = useStore();
  return useSyncExternalStore(store.subscribe, () => store.snapshot().below);
}

/** The sprint section whose "+ Add" row is open, and the calls to open or close one. */
export function useOpenSection() {
  const store = useStore();
  const open = useSyncExternalStore(store.subscribe, () => store.snapshot().section);
  return { open, openSection: store.openSection, closeSection: store.closeSection };
}

/** The add-below controls: open, close, stick the type and parent, and the typed text. */
export function useBelowControls() {
  const store = useStore();
  return {
    open: store.open,
    close: store.close,
    stick: store.stick,
    draft: store.draft,
  };
}

/**
 * Creates the typed rows one at a time (K2). `below` puts a row just under the last one made, or under the anchor
 * for the first, and moves the open row to it: a typed list keeps its order, even when Enter is pressed again before
 * the last write is answered. `inSprint` puts the first row at the top of a sprint, and each one after under the row
 * made. Both resolve false when nothing was created: `error` says why and a toast shows it.
 */
export function useAddChain() {
  const store = useStore();
  const error = useSyncExternalStore(store.subscribe, () => store.snapshot().error);
  // The last row an "+ Add" row made, so the next goes under it. Reset when the row closes.
  const made = useRef<string | null>(null);
  return {
    error,
    clearError: store.clearError,
    below: store.addBelow,
    inSprint: (sprintId: string | null, fields: AddFields) =>
      store.addInSprint(sprintId, made.current, fields, (id) => (made.current = id)),
    reset: () => {
      made.current = null;
      store.clearError();
    },
  };
}
