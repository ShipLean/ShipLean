import type {
  FeatureTimelineViewDto,
  TimelineBarDto,
  TimelineNotStartedDto,
  TimelinePersonDto,
  TimelineStoryDto,
} from '@shiplean/core/api/views';
import type { TimelineOptions } from '../TimelineOptions';

export type TimelineRow =
  | {
      kind: 'group';
      key: string;
      id: string;
      label: string;
      agent: boolean;
      person: TimelinePersonDto | null;
      tasks: number;
    }
  | { kind: 'bracket'; key: string; story: TimelineStoryDto }
  | { kind: 'bar'; key: string; bar: TimelineBarDto }
  | { kind: 'notStarted'; key: string; item: TimelineNotStartedDto };

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

/**
 * The rows the chart draws top to bottom. Stories: each started story's bracket, its bars, then its
 * not-started tasks; then each not-started story or bug followed by its tasks. Tasks: every bar, then
 * the not-started tasks. People: each person's bars then their not-started tasks, Unassigned last.
 */
export function timelineRows(
  view: FeatureTimelineViewDto,
  group: TimelineOptions['group'],
): TimelineRow[] {
  const bars = new Map(view.bars.map((bar) => [bar.key, bar]));
  const waiting = view.notStarted;
  if (group === 'stories') {
    const notStartedTasks = (parent: string) =>
      waiting
        .filter((item) => item.parent === parent)
        .map((item) => ({ kind: 'notStarted' as const, key: item.key, item }));
    const started = view.stories.flatMap((story) => [
      { kind: 'bracket' as const, key: story.key, story },
      ...story.tasks.flatMap((key) => {
        const bar = bars.get(key);
        return bar ? [{ kind: 'bar' as const, key, bar }] : [];
      }),
      ...notStartedTasks(story.key),
    ]);
    const unstarted = waiting
      .filter((item) => item.parent === null)
      .flatMap((item) => [
        { kind: 'notStarted' as const, key: item.key, item },
        ...notStartedTasks(item.key),
      ]);
    return [...started, ...unstarted];
  }
  const sorted = [...view.bars].sort(byStart);
  if (group === 'tasks')
    return [
      ...sorted.map((bar) => ({ kind: 'bar' as const, key: bar.key, bar })),
      ...waiting
        .filter((item) => item.parent !== null)
        .map((item) => ({ kind: 'notStarted' as const, key: item.key, item })),
    ];

  const groups = new Map<
    string,
    { person: TimelinePersonDto | null; bars: TimelineBarDto[]; waiting: TimelineNotStartedDto[] }
  >();
  const groupOf = (person: TimelinePersonDto | null) => {
    const id = personId(person);
    const entry = groups.get(id) ?? { person, bars: [], waiting: [] };
    groups.set(id, entry);
    return entry;
  };
  for (const bar of sorted) groupOf(bar.assignee).bars.push(bar);
  for (const item of waiting) if (item.parent !== null) groupOf(item.assignee).waiting.push(item);
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
      person: entry.person,
      tasks: entry.bars.length + entry.waiting.length,
    },
    ...entry.bars.map((bar) => ({ kind: 'bar' as const, key: bar.key, bar })),
    ...entry.waiting.map((item) => ({ kind: 'notStarted' as const, key: item.key, item })),
  ]);
}
