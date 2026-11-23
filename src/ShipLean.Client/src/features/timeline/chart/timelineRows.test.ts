import { describe, expect, it } from 'vitest';
import type { FeatureTimelineViewDto, TimelineBarDto } from '@shiplean/core/api/views';
import { FINISHED_TIMELINE, RUNNING_TIMELINE } from '../ct/stubFeatureTimeline';
import { timelineRows } from './timelineRows';

const withBars = (bars: TimelineBarDto[]): FeatureTimelineViewDto => ({
  ...FINISHED_TIMELINE,
  bars,
});
const bar = (
  key: string,
  name: string,
  userId: string | null,
  agent: boolean,
  startHours: number | null,
) =>
  ({
    ...FINISHED_TIMELINE.bars[0],
    key,
    startHours,
    assignee: { name, userId, agent },
  }) as TimelineBarDto;

describe('timelineRows', () => {
  it('Stories_ShouldPutEachBracketBeforeItsTasks', () => {
    const rows = timelineRows(FINISHED_TIMELINE, 'stories');
    expect(rows.slice(0, 5).map((r) => `${r.kind}:${r.key}`)).toEqual([
      'bracket:SHIP-185',
      'bar:SHIP-191',
      'bar:SHIP-192',
      'bar:SHIP-193',
      'bracket:SHIP-188',
    ]);
    expect(rows).toHaveLength(FINISHED_TIMELINE.stories.length + FINISHED_TIMELINE.bars.length);
  });

  it('Tasks_ShouldBeFlatByStartWithUnstartedLastThenByKey', () => {
    const view = withBars([
      bar('SHIP-3', 'A', null, true, null),
      bar('SHIP-2', 'A', null, true, 5),
      bar('SHIP-1', 'A', null, true, null),
      bar('SHIP-4', 'A', null, true, 1),
    ]);
    expect(timelineRows(view, 'tasks').map((r) => r.key)).toEqual([
      'SHIP-4',
      'SHIP-2',
      'SHIP-1',
      'SHIP-3',
    ]);
  });

  it('People_ShouldGroupAgentsByName', () => {
    const view = withBars([
      bar('SHIP-1', 'Claude Code', null, true, 1),
      bar('SHIP-2', 'Claude', null, true, 2),
      bar('SHIP-3', 'Claude Code', null, true, 3),
      bar('SHIP-4', 'Ana', 'u1', false, 4),
    ]);
    const rows = timelineRows(view, 'people');
    const groups = rows.filter((r) => r.kind === 'group');
    expect(groups.map((g) => (g.kind === 'group' ? `${g.label}:${g.agent}` : ''))).toEqual([
      'Claude Code:true',
      'Claude:true',
      'Ana:false',
    ]);
    expect(rows.map((r) => (r.kind === 'group' ? r.label : r.key))).toEqual([
      'Claude Code',
      'SHIP-1',
      'SHIP-3',
      'Claude',
      'SHIP-2',
      'Ana',
      'SHIP-4',
    ]);
  });

  it('People_ShouldPutUnassignedLast', () => {
    const unassigned = { ...bar('SHIP-9', 'x', null, false, 0), assignee: null };
    const view = withBars([unassigned, bar('SHIP-1', 'Ana', 'u1', false, 4)]);
    const labels = timelineRows(view, 'people').flatMap((r) =>
      r.kind === 'group' ? [r.label] : [],
    );
    expect(labels).toEqual(['Ana', 'Unassigned']);
  });

  it('Stories_WhenTaskNotStarted_ShouldSitUnderItsStoryAfterItsStartedTasks', () => {
    const rows = timelineRows(RUNNING_TIMELINE, 'stories').map((r) => `${r.kind}:${r.key}`);
    const story = rows.indexOf('bracket:SHIP-141');
    expect(rows.slice(story, story + 4)).toEqual([
      'bracket:SHIP-141',
      'bar:SHIP-171',
      'bar:SHIP-172',
      'notStarted:SHIP-173',
    ]);
  });

  it('Stories_WhenStoryNotStarted_ShouldFollowTheStartedStoriesWithItsTasks', () => {
    const [story] = RUNNING_TIMELINE.notStarted.filter((i) => i.parent === null);
    const [task] = RUNNING_TIMELINE.notStarted.filter((i) => i.parent !== null);
    const view: FeatureTimelineViewDto = {
      ...FINISHED_TIMELINE,
      notStarted: [
        { ...story, key: 'SHIP-50' },
        { ...task, key: 'SHIP-51', parent: 'SHIP-50' },
        { ...task, key: 'SHIP-52', parent: 'SHIP-185' },
      ],
    };
    const rows = timelineRows(view, 'stories').map((r) => `${r.kind}:${r.key}`);
    const started = timelineRows(FINISHED_TIMELINE, 'stories').length;
    expect(rows).toHaveLength(started + 3);
    expect(rows.slice(-2)).toEqual(['notStarted:SHIP-50', 'notStarted:SHIP-51']);
    expect(rows.slice(0, 5)).toEqual([
      'bracket:SHIP-185',
      'bar:SHIP-191',
      'bar:SHIP-192',
      'bar:SHIP-193',
      'notStarted:SHIP-52',
    ]);
  });

  it('Stories_ShouldShowRunningFixtureNotStartedStoryLast', () => {
    const rows = timelineRows(RUNNING_TIMELINE, 'stories');
    expect(`${rows.at(-1)!.kind}:${rows.at(-1)!.key}`).toBe('notStarted:SHIP-158');
  });

  it('Tasks_ShouldPutNotStartedTasksAfterEveryBar', () => {
    const rows = timelineRows(RUNNING_TIMELINE, 'tasks');
    expect(rows.map((r) => `${r.kind}:${r.key}`)).toEqual([
      ...RUNNING_TIMELINE.bars.map((b) => `bar:${b.key}`),
      'notStarted:SHIP-173',
    ]);
  });

  it('People_ShouldPutNotStartedTasksUnderTheirAssigneeAndUnassignedLast', () => {
    const [story, task] = RUNNING_TIMELINE.notStarted;
    const ana = story.assignee!;
    const view: FeatureTimelineViewDto = {
      ...RUNNING_TIMELINE,
      notStarted: [
        story,
        { ...task, key: 'SHIP-901', assignee: ana },
        { ...task, key: 'SHIP-902', assignee: null },
      ],
    };
    const rows = timelineRows(view, 'people');
    const labels = rows.flatMap((r) => (r.kind === 'group' ? [r.label] : []));
    expect(labels.at(-1)).toBe('Unassigned');
    const keysUnder = (label: string) => {
      const start = rows.findIndex((r) => r.kind === 'group' && r.label === label);
      const next = rows.findIndex((r, i) => i > start && r.kind === 'group');
      return rows.slice(start + 1, next === -1 ? undefined : next).map((r) => `${r.kind}:${r.key}`);
    };
    expect(keysUnder(ana.name)).toContain('notStarted:SHIP-901');
    expect(keysUnder('Unassigned')).toEqual(['notStarted:SHIP-902']);
    expect(rows.some((r) => r.key === story.key)).toBe(false);
  });
});
