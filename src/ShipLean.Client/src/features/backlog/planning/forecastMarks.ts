import type { ForecastDto } from '../../../api/sprints';

/** The label each forecast cut line carries, by the id of the item the line is drawn under: "→ Sprint 26". */
export function forecastMarks(forecast: ForecastDto | null): ReadonlyMap<string, string> {
  const marks = new Map<string, string>();
  for (const sprint of forecast?.sprints ?? []) {
    if (sprint.lastItemId !== null) marks.set(sprint.lastItemId, sprint.name);
  }
  return marks;
}
