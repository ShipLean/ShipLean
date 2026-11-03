import { createContext, useContext, useEffect, useMemo, useRef, type ReactNode } from 'react';

export type ListCommandName =
  | 'focusNext'
  | 'focusPrev'
  | 'toggleSelect'
  | 'clear'
  | 'open'
  | 'estimate'
  | 'moveMenu'
  | 'addBelow'
  | 'search'
  | 'help';

export type ListCommandHandler = (arg?: unknown) => void;

interface ListCommandRegistry {
  register: (name: ListCommandName, handler: ListCommandHandler) => () => void;
  run: (name: ListCommandName, arg?: unknown) => void;
}

const ListCommandsContext = createContext<ListCommandRegistry | null>(null);

/** Holds the screen's commands: the owning feature registers a handler, the keys and menus run it by name. */
export function ListCommandsProvider({ children }: { children: ReactNode }) {
  const handlers = useRef(new Map<ListCommandName, ListCommandHandler[]>());
  const registry = useMemo<ListCommandRegistry>(
    () => ({
      register: (name, handler) => {
        handlers.current.set(name, [...(handlers.current.get(name) ?? []), handler]);
        return () => {
          handlers.current.set(
            name,
            (handlers.current.get(name) ?? []).filter((h) => h !== handler),
          );
        };
      },
      run: (name, arg) => handlers.current.get(name)?.at(-1)?.(arg),
    }),
    [],
  );
  return <ListCommandsContext.Provider value={registry}>{children}</ListCommandsContext.Provider>;
}

/** Registers `handler` as the command `name` while mounted. The last registration wins; unmounting gives the name back to the one before. */
export function useListCommand(name: ListCommandName, handler: ListCommandHandler) {
  const registry = useContext(ListCommandsContext);
  const latest = useRef(handler);
  useEffect(() => {
    latest.current = handler;
  });
  useEffect(() => registry?.register(name, (arg) => latest.current(arg)), [registry, name]);
}

/** Runs a command by name. A name nobody registered, or no provider, does nothing. */
export function useRunListCommand(): (name: ListCommandName, arg?: unknown) => void {
  const registry = useContext(ListCommandsContext);
  return useMemo(() => (name, arg) => registry?.run(name, arg), [registry]);
}
