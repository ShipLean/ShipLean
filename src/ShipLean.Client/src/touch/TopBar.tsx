import {
  createContext,
  useContext,
  useLayoutEffect,
  useState,
  useSyncExternalStore,
  type ReactNode,
} from 'react';

/** What a page puts in the touch top bar (K4). Every key is optional; a page sets only the ones it needs. */
export interface TopBarSlots {
  /** Replaces the breadcrumb. */
  title?: ReactNode;
  /** A ‹ back link on the left; Menu moves to the right (C13). */
  back?: { to: string; label: string };
  /** One control between the presence and Search. */
  action?: ReactNode;
  /** An edit toolbar that replaces the whole bar while it is set (rule 12). */
  pinned?: ReactNode;
}

type SlotKey = keyof TopBarSlots;
const SLOT_KEYS: readonly SlotKey[] = ['title', 'back', 'action', 'pinned'];

interface Claim {
  owner: symbol;
  value: unknown;
}

// Slots merge per key. Each key keeps the callers that set it in the order they first set it; the last one shows.
// A caller updating its value keeps its place, and a caller that stops setting a key, or unmounts, drops only its own
// claims, so another caller's keys stay.
class TopBarStore {
  private readonly claims = new Map<SlotKey, Claim[]>();
  private readonly listeners = new Set<() => void>();
  private current: TopBarSlots = {};

  subscribe = (listener: () => void) => {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  };

  snapshot = () => this.current;

  set(owner: symbol, slots: TopBarSlots) {
    let changed = false;
    for (const key of SLOT_KEYS) {
      const value = slots[key];
      const list = this.claims.get(key) ?? [];
      const index = list.findIndex((claim) => claim.owner === owner);
      if (value === undefined) {
        if (index >= 0) {
          list.splice(index, 1);
          changed = true;
        }
      } else if (index < 0) {
        list.push({ owner, value });
        changed = true;
      } else if (!sameValue(key, list[index]!.value, value)) {
        list[index] = { owner, value };
        changed = true;
      }
      this.claims.set(key, list);
    }
    if (changed) this.publish();
  }

  clear(owner: symbol) {
    this.set(owner, {});
  }

  private publish() {
    const next: Record<string, unknown> = {};
    for (const key of SLOT_KEYS) {
      const list = this.claims.get(key);
      const last = list?.[list.length - 1];
      if (last) next[key] = last.value;
    }
    this.current = next as TopBarSlots;
    for (const listener of this.listeners) listener();
  }
}

// `back` is a fresh object on every render of the page that sets it; compare it by its fields so the bar doesn't
// re-render for nothing.
function sameValue(key: SlotKey, a: unknown, b: unknown): boolean {
  if (a === b) return true;
  if (key !== 'back') return false;
  const [x, y] = [a as TopBarSlots['back'], b as TopBarSlots['back']];
  return x?.to === y?.to && x?.label === y?.label;
}

const TopBarContext = createContext<TopBarStore | undefined>(undefined);

/** Holds the top bar's slots for everything under it. Shell.tsx mounts it once, at every width. */
export function TopBarProvider({ children }: { children: ReactNode }) {
  const [store] = useState(() => new TopBarStore());
  return <TopBarContext.Provider value={store}>{children}</TopBarContext.Provider>;
}

/**
 * Puts this caller's slots in the touch top bar while it is mounted. Only the keys it sets are its own: another caller
 * on the same page can set the others, the caller that set a key last shows, and unmounting removes only this caller's
 * keys. Above 900px nothing shows them. Outside a TopBarProvider (a page rendered on its own in a test) they go nowhere.
 */
export function useTopBar(slots: TopBarSlots): void {
  const store = useContext(TopBarContext);
  const [owner] = useState(() => Symbol('useTopBar'));
  // Every render: the page's slots are fresh nodes each time. Writing to the store re-renders only the bar.
  useLayoutEffect(() => {
    store?.set(owner, slots);
  });
  useLayoutEffect(() => {
    return () => store?.clear(owner);
  }, [store, owner]);
}

/** The merged slots, for the bar itself. */
export function useTopBarSlots(): TopBarSlots {
  const store = useContext(TopBarContext);
  if (store === undefined) throw new Error('useTopBarSlots needs a TopBarProvider');
  return useSyncExternalStore(store.subscribe, store.snapshot);
}
