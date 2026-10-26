import { createContext, useContext, useEffect, useRef, useState } from 'react';

/** A check's answer: nothing to say, or a message the user confirms before the save goes ahead. */
export type BeforeSaveResult = { confirm: string } | null;
export type BeforeSaveCheck = () => BeforeSaveResult;

export interface BeforeSaveRegistry {
  /** Adds a check; returns the function that removes it. */
  register: (check: BeforeSaveCheck) => () => void;
}

export const BeforeSaveContext = createContext<BeforeSaveRegistry | null>(null);

/**
 * Runs `check` each time Save is pressed, before anything is sent. A `{ confirm }` answer holds the save until the user
 * confirms; the latest `check` passed is the one that runs. Only inside a work item form.
 */
export function useBeforeSave(check: BeforeSaveCheck) {
  const registry = useContext(BeforeSaveContext);
  if (registry === null) throw new Error('useBeforeSave must be called inside a work item form.');
  const latest = useRef(check);
  useEffect(() => {
    latest.current = check;
  });
  useEffect(() => registry.register(() => latest.current()), [registry]);
}

/**
 * The form's side of the hook: the registry the form provides, and the save gate. `request(save)` runs every check:
 * with nothing to confirm it saves at once, otherwise `pending` holds the messages (asked once, together) until
 * `confirm(save)` saves or `cancel()` drops the save.
 */
export function useBeforeSaveGate() {
  const [checks] = useState(() => new Set<BeforeSaveCheck>());
  const [registry] = useState<BeforeSaveRegistry>(() => ({
    register: (check) => {
      checks.add(check);
      return () => {
        checks.delete(check);
      };
    },
  }));
  const [pending, setPending] = useState<string[] | null>(null);
  return {
    registry,
    pending,
    request: (save: () => void) => {
      const messages = [...checks].flatMap((check) => {
        const result = check();
        return result ? [result.confirm] : [];
      });
      if (messages.length === 0) {
        setPending(null);
        save();
      } else setPending(messages);
    },
    confirm: (save: () => void) => {
      setPending(null);
      save();
    },
    cancel: () => setPending(null),
  };
}
