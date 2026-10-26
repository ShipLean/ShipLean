import type { SprintDto } from '../../../api/sprints';

/** The sprint the prompt is about: the most recently ended one whose rollover nobody has decided. */
export function sprintToWrapUp(sprints: SprintDto[]): SprintDto | undefined {
  return sprints
    .filter((sprint) => sprint.status === 'past' && sprint.rolloverStatus !== 'decided')
    .sort((a, b) => b.endDate.localeCompare(a.endDate))[0];
}
