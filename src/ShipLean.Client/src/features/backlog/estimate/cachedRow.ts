import type { QueryClient } from '@tanstack/react-query';
import type { BacklogRowDto, BacklogViewDto } from '../../../api/views';

/** A row as the backlog queries hold it now, with the project whose backlog holds it. */
export interface CachedRow {
  row: BacklogRowDto;
  projectId: string;
}

/** The newest copy of a row in the cached backlog reads (any mode), or null when none holds it. A send takes its points and revision from here, never from a render's closure. */
export function cachedRow(queryClient: QueryClient, id: string): CachedRow | null {
  let found: CachedRow | null = null;
  const views = queryClient.getQueriesData<BacklogViewDto>({
    predicate: (query) => query.queryKey[0] === 'projects' && query.queryKey[2] === 'backlog',
  });
  for (const [, view] of views) {
    if (!view) continue;
    const rows = [...view.rows, ...view.sections.flatMap((section) => section.rows)];
    for (const row of rows) {
      if (row.id === id && (found === null || row.revision > found.row.revision)) {
        found = { row, projectId: view.projectId };
      }
    }
  }
  return found;
}
