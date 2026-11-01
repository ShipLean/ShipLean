import type { SprintDto } from '../../../../api/sprints';

/** The sprint an action item is pulled into: the earliest sprint that has not started, or undefined when there is none. */
export const nextSprintOf = (sprints: readonly SprintDto[]): SprintDto | undefined =>
  sprints
    .filter((s) => s.status === 'future')
    .sort((a, b) => a.startDate.localeCompare(b.startDate))[0];
