import type { RetroPointDto, RetroSearchDto } from '../../../../api/retros';

/** The row highlighted in the open retro: the clicked point when it is a result of that retro, else the retro's first row. */
export function highlightedRow(
  search: RetroSearchDto,
  pointId: string | undefined,
): RetroPointDto | undefined {
  const retroId = search.selected?.id;
  if (!retroId) return undefined;
  const inRetro = search.rows.filter((row) => row.retroId === retroId);
  return inRetro.find((row) => row.id === pointId) ?? inRetro[0];
}

const normalize = (text: string) => text.trim().replace(/\s+/g, ' ').toLowerCase();

/** Whether a search for `q` matched the root cause itself: the whole query, trimmed and in any case, appears in it (as Highlight marks it). A word that only some other field holds does not count. */
export function matchesRootCause(rootCause: string | null, q: string | undefined): boolean {
  if (!rootCause || !q) return false;
  const needle = normalize(q);
  return needle !== '' && normalize(rootCause).includes(needle);
}
