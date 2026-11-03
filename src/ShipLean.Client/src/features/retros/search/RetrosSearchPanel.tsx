import { useEffect, useRef } from 'react';
import { useSearchParams } from 'react-router-dom';
import type { ProjectDto } from '../../../api/projects';
import type { RetroDecision } from '../../../api/retros';
import type { RetroSearchQuery } from '../../../api/retrosApi';
import { RetroActionDialogProvider } from '../../retroShared/actionDialog/RetroActionDialogProvider';
import { pagesTestIds } from '../../pages/testIds';
import { RetroQueryState } from '../RetroQueryState';
import { retrosTestIds as ids } from '../testIds';
import { useRetroSearch } from '../useRetroQueries';
import { SearchFilters } from './SearchFilters';
import { SearchRows } from './SearchRows';
import { SelectedRetro } from './SelectedRetro';

const decisions: RetroDecision[] = ['undecided', 'actioned', 'notNeeded'];

const TEXT_PARAMS = ['q', 'sprint', 'category', 'theme', 'role', 'selected'] as const;

// The filters the address holds: ?q&sprint&category&theme&level&role&recurring&selected&noSprint&decision&unthemed. Anything else is ignored.
export function queryOf(params: URLSearchParams): RetroSearchQuery {
  const query: RetroSearchQuery = {};
  for (const name of TEXT_PARAMS) {
    const value = params.get(name);
    if (value) query[name] = value;
  }
  const level = params.get('level');
  if (level === 'stories' || level === 'features') query.level = level;
  if (params.get('recurring') === 'true') query.recurring = true;
  if (params.get('noSprint') === 'true') query.noSprint = true;
  if (params.get('unthemed') === 'true') query.unthemed = true;
  const decision = params.get('decision');
  if (decisions.includes(decision as RetroDecision)) query.decision = decision as RetroDecision;
  return query;
}

function paramsOf(query: RetroSearchQuery, rest: URLSearchParams): URLSearchParams {
  const params = new URLSearchParams(rest);
  for (const name of [...TEXT_PARAMS, 'level', 'recurring', 'noSprint', 'decision', 'unthemed'])
    params.delete(name);
  for (const name of TEXT_PARAMS) {
    const value = query[name];
    if (value) params.set(name, value);
  }
  if (query.level) params.set('level', query.level);
  if (query.recurring) params.set('recurring', 'true');
  if (query.noSprint) params.set('noSprint', 'true');
  if (query.decision) params.set('decision', query.decision);
  if (query.unthemed) params.set('unthemed', 'true');
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

  // Two changes made before the screen re-renders (a router transition) both build on the address as the first one left it,
  // not on the address of the last render, or the second would overwrite the first.
  const latest = useRef(params);
  useEffect(() => {
    latest.current = params;
  }, [params]);
  const current = () => queryOf(latest.current);

  // `point` is client-only: it rides along with `selected` and is dropped whenever the open retro changes otherwise.
  const write = (next: RetroSearchQuery, replace: boolean, point?: string) => {
    const written = paramsOf(next, latest.current);
    written.delete('point');
    if (point) written.set('point', point);
    latest.current = written;
    setParams(written, { replace });
  };
  return (
    <RetroActionDialogProvider projectId={project.id}>
      <RetroQueryState query={result}>
        {(search) => {
          const props = {
            project,
            query,
            search,
            pointId: params.get('point') ?? undefined,
            // Changing a filter closes the open retro, which the new result may not hold; typing replaces the entry.
            change: (patch: Partial<RetroSearchQuery>) =>
              write({ ...current(), selected: undefined, ...patch }, 'q' in patch),
            select: (retroId: string | null, pointId?: string) =>
              write({ ...current(), selected: retroId ?? undefined }, false, pointId),
          };
          return (
            <div data-testid={ids.retrosSearchPanel} className="grid gap-4">
              <div data-testid={pagesTestIds.slotRetroSearchFilters}>
                <SearchFilters {...props} />
              </div>
              <div className="grid grid-cols-[minmax(0,1fr)_minmax(0,380px)] gap-4">
                <SearchRows {...props} />
                <SelectedRetro {...props} />
              </div>
            </div>
          );
        }}
      </RetroQueryState>
    </RetroActionDialogProvider>
  );
}
