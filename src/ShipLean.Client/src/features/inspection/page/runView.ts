import type { RunAxisDto, RunOverviewTurnDto, RunTurnDto } from '@shiplean/core/api/inspection';

/** The axis of one turn, from the overview's percentages of the whole run; the whole axis when the turn is unknown. */
export function zoomedAxis(
  axis: RunAxisDto,
  overview: RunOverviewTurnDto[],
  zoomTurn: number | null,
): RunAxisDto {
  const turn = zoomTurn === null ? undefined : overview.find((t) => t.turn === zoomTurn);
  if (turn === undefined) return axis;
  const total = axis.endMs - axis.startMs;
  return {
    startMs: axis.startMs + (turn.startPct / 100) * total,
    endMs: axis.startMs + (turn.endPct / 100) * total,
  };
}

/** What the page lays out: only the zoomed turn, and only the rows whose name holds the find text. */
export function visibleTurns(
  turns: RunTurnDto[],
  zoomTurn: number | null,
  query: string,
): RunTurnDto[] {
  const needle = query.trim().toLowerCase();
  const inZoom = turns.filter((t) => zoomTurn === null || t.number === zoomTurn);
  if (needle === '') return inZoom;
  return inZoom.map((t) => ({
    ...t,
    rows: t.rows.filter((r) => r.name.toLowerCase().includes(needle)),
  }));
}
