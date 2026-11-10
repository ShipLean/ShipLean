// Pure rules of the Retro Notes view: which sprint, which category opens, and how a category's points are ordered.
import type { UserRef } from '@shiplean/core/api/refs';
import {
  RETRO_CATEGORIES,
  type RetroAuthorDto,
  type RetroCategory,
  type RetroCategoryColumnDto,
  type RetroPointDto,
  type RetroThemeGroupDto,
  type SprintRetroDto,
} from '@shiplean/core/api/retros';
import type { SprintDto } from '@shiplean/core/api/sprints';
import type { Pending } from '../../actions/types';

export const CATEGORY_LABELS: Record<RetroCategory, string> = {
  wentWell: 'Went well',
  notInPlan: 'Not in the plan',
  wentBadly: 'Went badly',
  improve: 'Can be improved',
};

// The columns in the web's order, whatever order the server sent them in.
export function orderedColumns(retro: SprintRetroDto): RetroCategoryColumnDto[] {
  return RETRO_CATEGORIES.flatMap((category) =>
    retro.categories.filter((c) => c.category === category),
  );
}

export const countPoints = (retro: SprintRetroDto): number =>
  retro.categories.reduce(
    (sum, column) => sum + column.groups.reduce((n, group) => n + group.points.length, 0),
    0,
  );

// D2: the first category with points to decide, else Went well.
export function openingCategory(retro: SprintRetroDto): RetroCategory {
  return orderedColumns(retro).find((column) => column.toDecide > 0)?.category ?? 'wentWell';
}

// The current sprint, and the latest one that has ended.
export function retroSprints(sprints: SprintDto[]): {
  current: SprintDto | null;
  ended: SprintDto | null;
} {
  const past = sprints
    .filter((s) => s.status === 'past')
    .sort((a, b) => (a.endDate < b.endDate ? 1 : a.endDate > b.endDate ? -1 : 0));
  return {
    current: sprints.find((s) => s.status === 'current') ?? null,
    ended: past[0] ?? null,
  };
}

// P-R2: the current sprint, but the ended one while the current one has no point yet. `currentPoints` is null until
// the current sprint's retro has loaded.
export function shownSprint(sprints: SprintDto[], currentPoints: number | null): SprintDto | null {
  const { current, ended } = retroSprints(sprints);
  if (current === null) return ended;
  if (currentPoints === 0) return ended ?? current;
  return current;
}

// Themes with the most points first, the unthemed group last; within a theme, points with more supporters first (D4:
// +1 only sorts), then the server's order.
export function themeGroups(column: RetroCategoryColumnDto): RetroThemeGroupDto[] {
  return column.groups
    .filter((group) => group.points.length > 0)
    .map((group, index) => ({ group, index }))
    .sort((a, b) => {
      if ((a.group.theme === null) !== (b.group.theme === null))
        return a.group.theme === null ? 1 : -1;
      return b.group.points.length - a.group.points.length || a.index - b.index;
    })
    .map(({ group }) => ({
      ...group,
      points: group.points
        .map((point, index) => ({ point, index }))
        .sort((a, b) => b.point.supporters.length - a.point.supporters.length || a.index - b.index)
        .map(({ point }) => point),
    }));
}

// The point as the person sees it: their own +1, taken from what they just tapped until the server's read catches up.
export function withMySupport(
  point: RetroPointDto,
  me: UserRef | null,
  pending: Pending[],
): RetroPointDto {
  if (me === null) return point;
  const mine = pending.filter(
    (p) =>
      p.action.area === 'retro' &&
      (p.action.kind === 'support' || p.action.kind === 'unsupport') &&
      'pointId' in p.action &&
      p.action.pointId === point.id &&
      p.state !== 'failed' &&
      p.undoOutcome === null,
  );
  const last = mine.at(-1);
  if (last === undefined) return point;
  const others = point.supporters.filter((u) => u.id !== me.id);
  return last.action.kind === 'support'
    ? { ...point, supportedByMe: true, supporters: [...others, me] }
    : { ...point, supportedByMe: false, supporters: others };
}

// A person is their name. An agent is its name with the lane as a chip: "Claude Code · lane C" is "Claude Code" and
// "lane C"; an agent name without a lane takes the role's words.
export function authorLabel(author: RetroAuthorDto): { name: string; lane: string | null } {
  if (author.agentName === null) return { name: author.user.name, lane: null };
  const [name, ...rest] = author.agentName.split('·').map((part) => part.trim());
  const lane = rest.join(' · ');
  return { name, lane: lane === '' ? (author.role === 'conductor' ? 'conductor' : null) : lane };
}
