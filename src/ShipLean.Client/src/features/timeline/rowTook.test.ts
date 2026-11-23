import { describe, expect, it } from 'vitest';
import { FINISHED_TIMELINE, RUNNING_TIMELINE } from './ct/stubFeatureTimeline';
import { timelineRows, type TimelineRow } from './chart/timelineRows';
import { rowTook } from './rowTook';

const find = (rows: TimelineRow[], key: string) => rows.find((r) => r.key === key)!;

describe('rowTook', () => {
  const finished = timelineRows(FINISHED_TIMELINE, 'stories');
  const running = timelineRows(RUNNING_TIMELINE, 'stories');

  it('Story_ShouldShowItsDurationLabel', () => {
    expect(rowTook(find(finished, 'SHIP-185'), FINISHED_TIMELINE)).toEqual({ text: '2.1 days' });
  });

  it('Bar_ShouldShowItsDurationLabel', () => {
    expect(rowTook(find(finished, 'SHIP-192'), FINISHED_TIMELINE)).toEqual({ text: '1 day' });
  });

  it('Bar_WhenInferred_ShouldSayNoStart', () => {
    expect(rowTook(find(finished, 'SHIP-198'), FINISHED_TIMELINE)).toEqual({ text: 'No start' });
  });

  it('Bar_WhenInFlight_ShouldShowOpenLabelWithTitle', () => {
    expect(rowTook(find(running, 'SHIP-172'), RUNNING_TIMELINE)).toEqual({
      text: '1.9 days ▸',
      open: true,
      title: 'Still open: so far',
    });
  });

  it('NotStarted_ShouldSayNotStartedAndItalicOnlyForStories', () => {
    expect(rowTook(find(running, 'SHIP-173'), RUNNING_TIMELINE)).toEqual({
      text: 'Not started',
      italic: false,
    });
    expect(rowTook(find(running, 'SHIP-158'), RUNNING_TIMELINE)).toEqual({
      text: 'Not started',
      italic: true,
    });
  });

  it('Group_ShouldCountItsTasks', () => {
    const people = timelineRows(FINISHED_TIMELINE, 'people');
    const texts = people.filter((r) => r.kind === 'group').map((r) => rowTook(r, FINISHED_TIMELINE).text);
    expect(texts).toEqual(['5 tasks', '2 tasks', '1 task', '1 task']);
  });
});
