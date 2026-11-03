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

/** Whether a search for `q` matched the root cause: any word of it appears there, in any case. */
export function matchesRootCause(rootCause: string | null, q: string | undefined): boolean {
  if (!rootCause || !q) return false;
  const text = rootCause.toLowerCase();
  return q
    .toLowerCase()
    .split(/\s+/)
    .some((word) => word !== '' && text.includes(word));
}
