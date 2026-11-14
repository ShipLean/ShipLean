import type {
  FeatureTimelineViewDto,
  TimelineBarDto,
  TimelinePersonDto,
  TimelineStoryDto,
} from '@shiplean/core/api/views';
import type { TimelineOptions } from '../TimelineOptions';

export type TimelineRow =
  | { kind: 'group'; key: string; id: string; label: string; agent: boolean }
  | { kind: 'bracket'; key: string; story: TimelineStoryDto }
  | { kind: 'bar'; key: string; bar: TimelineBarDto };

const UNASSIGNED = 'Unassigned';

/** A person is told apart by user id; an agent has none, so agents are told apart by name. */
const personId = (person: TimelinePersonDto | null): string =>
  person === null
    ? UNASSIGNED
    : person.agent
      ? `agent:${person.name}`
      : `user:${person.userId ?? person.name}`;

const byStart = (a: TimelineBarDto, b: TimelineBarDto): number => {
  if (a.startHours === null && b.startHours === null) return a.key.localeCompare(b.key);
  if (a.startHours === null) return 1;
  if (b.startHours === null) return -1;
  return a.startHours - b.startHours || a.key.localeCompare(b.key);
};

/** The rows the chart draws top to bottom. Items that have not started are not rows. */
export function timelineRows(
  view: FeatureTimelineViewDto,
  group: TimelineOptions['group'],
): TimelineRow[] {
  const bars = new Map(view.bars.map((bar) => [bar.key, bar]));
  if (group === 'stories') {
    return view.stories.flatMap((story) => [
      { kind: 'bracket' as const, key: story.key, story },
      ...story.tasks.flatMap((key) => {
        const bar = bars.get(key);
        return bar ? [{ kind: 'bar' as const, key, bar }] : [];
      }),
    ]);
  }
  const sorted = [...view.bars].sort(byStart);
  if (group === 'tasks') return sorted.map((bar) => ({ kind: 'bar', key: bar.key, bar }));

  const groups = new Map<string, { person: TimelinePersonDto | null; bars: TimelineBarDto[] }>();
  for (const bar of sorted) {
    const id = personId(bar.assignee);
    const entry = groups.get(id) ?? { person: bar.assignee, bars: [] };
    entry.bars.push(bar);
    groups.set(id, entry);
  }
  const ordered = [...groups.entries()].sort(([a], [b]) =>
    a === UNASSIGNED ? 1 : b === UNASSIGNED ? -1 : 0,
  );
  return ordered.flatMap(([id, entry]) => [
    {
      kind: 'group' as const,
      key: `group:${id}`,
      id,
      label: entry.person?.name ?? UNASSIGNED,
      agent: entry.person?.agent ?? false,
    },
    ...entry.bars.map((bar) => ({ kind: 'bar' as const, key: bar.key, bar })),
  ]);
}
