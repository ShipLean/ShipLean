import { createContext, useContext } from 'react';

export interface QueueFixed {
  /** The queue's rows that are still on the list and no longer flagged by the open check. */
  fixed: ReadonlySet<string>;
  /** How many of the frozen ids still have a row. */
  total: number;
}

const NONE: QueueFixed = { fixed: new Set(), total: 0 };

export const QueueFixedContext = createContext<QueueFixed>(NONE);

/** The queue's tally; empty while no queue is open. */
export function useQueueFixed(): QueueFixed {
  return useContext(QueueFixedContext);
}

/** Whether the open queue has this row marked fixed. */
export function useIsFixed(rowId: string): boolean {
  return useContext(QueueFixedContext).fixed.has(rowId);
}
