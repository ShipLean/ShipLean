import type { SprintDto } from '../../../api/sprints';

/** The sprint that started last before this one, or undefined for the first. Dates are yyyy-MM-dd, so text order is date order. */
export function previousSprint(sprints: SprintDto[], sprint: SprintDto): SprintDto | undefined {
  return sprints
    .filter((other) => other.id !== sprint.id && other.startDate < sprint.startDate)
    .sort((a, b) => b.startDate.localeCompare(a.startDate))[0];
}
