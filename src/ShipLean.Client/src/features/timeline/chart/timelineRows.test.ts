import { describe, expect, it } from 'vitest';
import type { FeatureTimelineViewDto, TimelineBarDto } from '@shiplean/core/api/views';
import { FINISHED_TIMELINE } from '../ct/stubFeatureTimeline';
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
      bar('SHIP-2', 'Codex', null, true, 2),
      bar('SHIP-3', 'Claude Code', null, true, 3),
      bar('SHIP-4', 'Ana', 'u1', false, 4),
    ]);
    const rows = timelineRows(view, 'people');
    const groups = rows.filter((r) => r.kind === 'group');
    expect(groups.map((g) => (g.kind === 'group' ? `${g.label}:${g.agent}` : ''))).toEqual([
      'Claude Code:true',
      'Codex:true',
      'Ana:false',
    ]);
    expect(rows.map((r) => (r.kind === 'group' ? r.label : r.key))).toEqual([
      'Claude Code',
      'SHIP-1',
      'SHIP-3',
      'Codex',
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
});
