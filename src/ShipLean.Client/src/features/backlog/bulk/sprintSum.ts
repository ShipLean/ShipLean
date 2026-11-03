import type { BacklogRowDto, BacklogSectionDto } from '../../../api/views';
import type { VelocityDto } from '../../../api/sprints';

export interface SprintSum {
  /** The points the section holds now. */
  now: number;
  /** The points `rows` would add: rows already in the section add nothing, unestimated rows add 0. */
  adding: number;
  after: number;
  /** The limit: the section's capacity, else the team's velocity (override first); null when neither exists. */
  of: number | null;
  over: boolean;
  /** "13 + 8 = 21 / 22", without " / n" when there is no limit. The one place the estimate unit is formatted. */
  text: string;
}

/** What a sprint section would hold after `rows` join it. */
export function sprintSum(
  section: BacklogSectionDto,
  rows: readonly BacklogRowDto[],
  velocity: VelocityDto | null,
): SprintSum {
  const adding = rows
    .filter((row) => (row.sprint?.id ?? null) !== (section.sprint?.id ?? null))
    .reduce((sum, row) => sum + (row.points ?? 0), 0);
  const fromVelocity = velocity === null ? 0 : (velocity.override ?? velocity.average);
  const of = section.capacity ?? (fromVelocity > 0 ? fromVelocity : null);
  const now = section.points;
  const after = now + adding;
  return {
    now,
    adding,
    after,
    of,
    over: of !== null && after > of,
    text: `${now} + ${adding} = ${after}${of === null ? '' : ` / ${of}`}`,
  };
}
