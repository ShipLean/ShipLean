import type { ReactNode } from 'react';
import type { QueryKey } from '@tanstack/react-query';
import { BulkRunContext, useBulkRun } from './useBulkRun';

/** One bulk runner for a backlog mode, shared by everything on the screen that writes many rows. */
export function BulkRunProvider({
  projectId,
  queryKey,
  children,
}: {
  projectId: string;
  queryKey: QueryKey;
  children: ReactNode;
}) {
  const runner = useBulkRun(projectId, queryKey);
  return <BulkRunContext.Provider value={runner}>{children}</BulkRunContext.Provider>;
}
