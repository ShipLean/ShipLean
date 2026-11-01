import { useSearchParams } from 'react-router-dom';
import type { ProjectDto } from '../../../api/projects';
import type { RetroSearchQuery } from '../../../api/retrosApi';
import { pagesTestIds } from '../../pages/testIds';
import { RetroQueryState } from '../RetroQueryState';
import { retrosTestIds as ids } from '../testIds';
import { useRetroSearch } from '../useRetroQueries';
import { SearchFilters } from './SearchFilters';
import { SearchRows } from './SearchRows';
import { SelectedRetro } from './SelectedRetro';

const TEXT_PARAMS = ['q', 'sprint', 'category', 'theme', 'role', 'selected'] as const;

// The filters the address holds: ?q&sprint&category&theme&level&role&recurring&selected. Anything else is ignored.
export function queryOf(params: URLSearchParams): RetroSearchQuery {
  const query: RetroSearchQuery = {};
  for (const name of TEXT_PARAMS) {
    const value = params.get(name);
    if (value) query[name] = value;
  }
  const level = params.get('level');
  if (level === 'stories' || level === 'features') query.level = level;
  if (params.get('recurring') === 'true') query.recurring = true;
  return query;
}

function paramsOf(query: RetroSearchQuery, rest: URLSearchParams): URLSearchParams {
  const params = new URLSearchParams(rest);
  for (const name of [...TEXT_PARAMS, 'level', 'recurring']) params.delete(name);
  for (const name of TEXT_PARAMS) {
    const value = query[name];
    if (value) params.set(name, value);
  }
  if (query.level) params.set('level', query.level);
  if (query.recurring) params.set('recurring', 'true');
  return params;
}

/**
 * Retrospectives › Search. It owns the filters, which live in the address (so a search can be shared and Back works),
 * reads the result for them, and hands both to its three parts.
 */
export function RetrosSearchPanel({ project }: { project: ProjectDto }) {
  const [params, setParams] = useSearchParams();
  const query = queryOf(params);
  const result = useRetroSearch(project.id, query);

  // `point` is client-only: it rides along with `selected` and is dropped whenever the open retro changes otherwise.
  const write = (next: RetroSearchQuery, replace: boolean, point?: string) =>
    setParams(
      (prev) => {
        const written = paramsOf(next, prev);
        written.delete('point');
        if (point) written.set('point', point);
        return written;
      },
      { replace },
    );
  return (
    <RetroQueryState query={result}>
      {(search) => {
        const props = {
          project,
          query,
          search,
          pointId: params.get('point') ?? undefined,
          // Changing a filter closes the open retro, which the new result may not hold; typing replaces the entry.
          change: (patch: Partial<RetroSearchQuery>) =>
            write({ ...query, selected: undefined, ...patch }, 'q' in patch),
          select: (retroId: string | null, pointId?: string) =>
            write({ ...query, selected: retroId ?? undefined }, false, pointId),
        };
        return (
          <div data-testid={ids.retrosSearchPanel} className="grid gap-4">
            <div data-testid={pagesTestIds.slotRetroSearchFilters}>
              <SearchFilters {...props} />
            </div>
            <div className="grid grid-cols-[minmax(0,1fr)_minmax(0,28rem)] gap-4">
              <SearchRows {...props} />
              <SelectedRetro {...props} />
            </div>
          </div>
        );
      }}
    </RetroQueryState>
  );
}
