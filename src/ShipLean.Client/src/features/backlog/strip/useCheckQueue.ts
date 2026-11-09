import { useCallback, useMemo, useState } from 'react';
import type { BacklogCheckId } from '@shiplean/core/api/backlogWrites';
import type { BacklogRowDto } from '@shiplean/core/api/views';

export interface CheckQueue {
  /** The rows of the check being worked through, in their order. */
  apply: (rows: BacklogRowDto[]) => BacklogRowDto[];
  /** A check is being worked through. */
  active: boolean;
  /** The check being worked through. */
  checkId: BacklogCheckId | null;
  /** The ids the queue froze when the check was clicked. */
  frozenIds: readonly string[];
  /** Work through a check: the ids are frozen now, so rows stay as they are fixed. Opening the open check leaves it. */
  open: (checkId: BacklogCheckId, itemIds: readonly string[]) => void;
  /** Leave the queue. */
  exit: () => void;
}

interface Opened {
  checkId: BacklogCheckId;
  ids: readonly string[];
}

const NO_IDS: readonly string[] = [];

/** Working through the rows one "Before you plan" check flags. The ids are frozen at the click; an id with no row is dropped. */
export function useCheckQueue(): CheckQueue {
  const [opened, setOpened] = useState<Opened | null>(null);
  const open = useCallback(
    (checkId: BacklogCheckId, itemIds: readonly string[]) =>
      setOpened((current) =>
        current?.checkId === checkId ? null : { checkId, ids: [...itemIds] },
      ),
    [],
  );
  const exit = useCallback(() => setOpened(null), []);
  return useMemo<CheckQueue>(() => {
    if (!opened) {
      return {
        apply: (rows) => rows,
        active: false,
        checkId: null,
        frozenIds: NO_IDS,
        open,
        exit,
      };
    }
    const frozen = new Set(opened.ids);
    return {
      apply: (rows) => rows.filter((row) => frozen.has(row.id)),
      active: true,
      checkId: opened.checkId,
      frozenIds: opened.ids,
      open,
      exit,
    };
  }, [opened, open, exit]);
}
