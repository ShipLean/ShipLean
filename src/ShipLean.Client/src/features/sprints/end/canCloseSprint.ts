import type { SprintDto } from '../../../api/sprints';

/** The Close sprint button shows on a running or ended sprint nobody has closed. A future sprint has nothing to close. */
export function canCloseSprint(sprint: SprintDto): boolean {
  return sprint.status !== 'future' && sprint.rolloverStatus !== 'decided';
}
