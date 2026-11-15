import type { BacklogRowDto } from '@shiplean/core/api/views';

/**
 * The Forecast sprint each row would land in, by row id, for "→ Sprint n" on every touch row. `marks` names the sprint
 * by the id of its last item (forecastMarks), so a row takes the name of the first mark at or below it; rows past the
 * last mark go in no forecast sprint and get none.
 */
export function forecastLabels(
  rows: readonly BacklogRowDto[],
  marks: ReadonlyMap<string, string>,
): ReadonlyMap<string, string> {
  const labels = new Map<string, string>();
  let run: string[] = [];
  for (const row of rows) {
    run.push(row.id);
    const name = marks.get(row.id);
    if (name === undefined) continue;
    for (const id of run) labels.set(id, name);
    run = [];
  }
  return labels;
}
