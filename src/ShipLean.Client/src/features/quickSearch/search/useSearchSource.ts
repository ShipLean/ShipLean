import { useMemo } from 'react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { fetchSearch } from '../../../api/search';
import { useProjects } from '../../shell/useShellQueries';
import { useShellProject } from '../../shell/useShellProject';
import { useDebouncedValue } from '../../workItem/form/description/useDebouncedValue';
import type { QuickSearchResults } from '../types';
import { mapSearchView, SEARCH_LIMIT } from './mapSearchView';

const DEBOUNCE_MS = 150;

const empty: QuickSearchResults = { groups: [], status: 'ready', note: null, retry: null };

// Results of GET /api/search for the typed query, closed items included. The query key holds the query, so a late
// answer to an older query never shows; the previous results stay until the next ones arrive.
export function useSearchSource(query: string): QuickSearchResults {
  const trimmed = query.trim();
  const debounced = useDebouncedValue(trimmed, DEBOUNCE_MS);
  const navigate = useNavigate();
  const { key: currentKey } = useShellProject();
  const projects = useProjects().data;
  const search = useQuery({
    queryKey: ['quickSearch', debounced],
    queryFn: ({ signal }) =>
      fetchSearch({ q: debounced, prefix: true, closed: true, limit: SEARCH_LIMIT }, signal),
    enabled: debounced !== '',
    placeholderData: keepPreviousData,
    retry: false,
  });
  const mapped = useMemo(() => {
    if (!search.data) return null;
    const memberCounts = projects
      ? new Map(projects.map((p) => [p.key, p.memberCount]))
      : undefined;
    return mapSearchView(search.data, { currentKey, memberCounts, navigate });
  }, [search.data, projects, currentKey, navigate]);

  if (trimmed === '') return empty;
  const pending = trimmed !== debounced || search.isFetching;
  if (search.isError && !pending) {
    return { groups: [], status: 'error', note: null, retry: () => void search.refetch() };
  }
  return {
    groups: mapped?.groups ?? [],
    status: pending ? 'loading' : 'ready',
    note: mapped?.note ?? null,
    retry: null,
  };
}
