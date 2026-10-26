import type { SprintDto } from '../../../api/sprints';

const order: Record<SprintDto['status'], number> = { current: 0, future: 1, past: 2 };

/** Current first, then the future (soonest first), then the past (latest first). Dates are yyyy-MM-dd, so text order is date order. */
export function orderSprints(sprints: SprintDto[]): SprintDto[] {
  return [...sprints].sort((a, b) => {
    if (a.status !== b.status) return order[a.status] - order[b.status];
    const byStart = a.startDate.localeCompare(b.startDate);
    return a.status === 'past' ? -byStart : byStart;
  });
}
