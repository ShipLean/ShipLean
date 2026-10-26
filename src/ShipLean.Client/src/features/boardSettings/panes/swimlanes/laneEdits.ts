import type { CriteriaJson, Swatch, SwimlaneJson } from '../../../../api/persisted';

/** The fields a card can be dropped into a criteria lane for: the board writes the lane's `=` value onto the card. */
const DROP_FIELDS = ['priority', 'severity', 'points', 'assignee', 'sprint'];

export const newLaneId = () => `lane-${crypto.randomUUID().slice(0, 8)}`;

/** A criteria lane takes drops only when it has clauses and every one is `=` on a field the board can write; other lanes always do. */
export function acceptsDrops(lane: SwimlaneJson): boolean {
  if (lane.kind !== 'criteria') return true;
  const clauses = lane.criteria?.clauses ?? [];
  return clauses.length > 0 && clauses.every((c) => c.op === '=' && DROP_FIELDS.includes(c.field));
}

export function blankLane(): SwimlaneJson {
  return { id: newLaneId(), name: 'New lane', colour: null, kind: 'manual', criteria: null };
}

export const replaceLane = (lanes: SwimlaneJson[], next: SwimlaneJson): SwimlaneJson[] =>
  lanes.map((l) => (l.id === next.id ? next : l));

export function moveLane(lanes: SwimlaneJson[], from: number, to: number): SwimlaneJson[] {
  if (from === to || from < 0 || to < 0 || from >= lanes.length || to >= lanes.length) return lanes;
  const next = [...lanes];
  const [lane] = next.splice(from, 1);
  next.splice(to, 0, lane);
  return next;
}

/** An empty clause list is no criteria at all: the lane keeps its kind and takes no cards until a clause is added. */
export const withCriteria = (lane: SwimlaneJson, criteria: CriteriaJson): SwimlaneJson => ({
  ...lane,
  criteria: criteria.clauses.length === 0 ? null : criteria,
});

export const withColour = (lane: SwimlaneJson, colour: Swatch | null): SwimlaneJson => ({
  ...lane,
  colour,
});
