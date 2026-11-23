import { expect, test } from '@playwright/experimental-ct-react';
import type { FeatureTimelineViewDto } from '@shiplean/core/api/views';
import { GanttChart } from '../GanttChart';
import { DEFAULT_TIMELINE_OPTIONS, type TimelineOptions } from '../TimelineOptions';
import { timelineTestIds as ids } from '../testIds';
import { FINISHED_TIMELINE, RUNNING_TIMELINE } from './stubFeatureTimeline';

const mountChart = (
  mount: Parameters<Parameters<typeof test>[2]>[0]['mount'],
  view: FeatureTimelineViewDto,
  extra: { options?: Partial<TimelineOptions>; selected?: string | null; picks?: string[] } = {},
) =>
  mount(
    <GanttChart
      view={view}
      options={{ ...DEFAULT_TIMELINE_OPTIONS, ...extra.options }}
      selected={extra.selected ?? null}
      onSelect={(key) => {
        if (key !== null) extra.picks?.push(key);
      }}
    />,
  );

test('GanttRowLabels_WhenOnlyNotStartedItems_ShouldShowThemInsteadOfTheEmptyMessage', async ({
  mount,
  page,
}) => {
  const [story] = RUNNING_TIMELINE.notStarted;
  await mountChart(mount, {
    ...RUNNING_TIMELINE,
    bars: [],
    stories: [],
    notStarted: [story, { ...story, key: 'SHIP-159', type: 'bug' }],
  });
  const rows = page.getByTestId(ids.chartRow);
  await expect(rows).toHaveCount(2);
  await expect(page.getByText('Not started')).toHaveCount(2);
  await expect(page.getByText('Nothing has started yet.')).toHaveCount(0);
});

test('GanttRowLabels_WhenNothingAtAll_ShouldStillSayNothingStarted', async ({ mount, page }) => {
  await mountChart(mount, { ...RUNNING_TIMELINE, bars: [], stories: [], notStarted: [] });
  await expect(page.getByText('Nothing has started yet.')).toBeVisible();
  await expect(page.getByTestId(ids.chartRow)).toHaveCount(0);
});

test('GanttRowLabels_ShouldHeadTheColumnByGrouping', async ({ mount, page }) => {
  const head = page.getByTestId(ids.chartLeftHead);
  const component = await mountChart(mount, FINISHED_TIMELINE);
  await expect(head).toContainText('STORY · TASK');
  await expect(head).toContainText('TOOK');
  await component.update(
    <GanttChart
      view={FINISHED_TIMELINE}
      options={{ ...DEFAULT_TIMELINE_OPTIONS, group: 'people' }}
      selected={null}
      onSelect={() => undefined}
    />,
  );
  await expect(head).toContainText('PERSON · TASK');
  await component.update(
    <GanttChart
      view={FINISHED_TIMELINE}
      options={{ ...DEFAULT_TIMELINE_OPTIONS, group: 'tasks' }}
      selected={null}
      onSelect={() => undefined}
    />,
  );
  await expect(head).toContainText('TASK');
  await expect(head).not.toContainText('PERSON');
  await expect(head).not.toContainText('STORY');
});

test('GanttRowLabels_WhenInferred_ShouldSayNoStart', async ({ mount, page }) => {
  await mountChart(mount, FINISHED_TIMELINE);
  const r = page.getByTestId(ids.chartRow).and(page.locator('[data-key="SHIP-198"]'));
  await expect(r.getByTestId(ids.chartRowTook)).toHaveText('No start');
  await expect(r.getByText('SHIP-198')).toBeVisible();
  await expect(r.getByRole('img', { name: 'Task' })).toBeVisible();
});

test('GanttRowLabels_WhenBarOpen_ShouldShowSoFarWithArrow', async ({ mount, page }) => {
  await mountChart(mount, RUNNING_TIMELINE);
  const took = page
    .getByTestId(ids.chartRow)
    .and(page.locator('[data-key="SHIP-172"]'))
    .getByTestId(ids.chartRowTook);
  await expect(took).toHaveText('1.9 days ▸');
  await expect(took).toHaveAttribute('title', 'Still open: so far');
});

test('GanttRowLabels_WhenRunning_ShouldShowNotStartedRows', async ({ mount, page }) => {
  await mountChart(mount, RUNNING_TIMELINE);
  const rows = page.getByTestId(ids.chartRow);
  const keys = await rows.evaluateAll((els) => els.map((el) => el.getAttribute('data-key')));
  const task = rows.and(page.locator('[data-key="SHIP-173"]'));
  await expect(task.getByTestId(ids.chartRowTook)).toHaveText('Not started');
  expect(keys.indexOf('SHIP-173')).toBe(keys.indexOf('SHIP-172') + 1);
  const story = rows.and(page.locator('[data-key="SHIP-158"]'));
  await expect(story.getByTestId(ids.chartRowTook)).toHaveText('Not started');
  await expect(story.getByRole('img', { name: 'Story' })).toBeVisible();
  expect(keys.at(-1)).toBe('SHIP-158');
});

test('GanttRowLabels_WhenGroupPeople_ShouldShowUnassignedGroup', async ({ mount, page }) => {
  await mountChart(mount, RUNNING_TIMELINE, { options: { group: 'people' } });
  const groups = page.getByTestId(ids.chartRow).and(page.locator('[data-kind="group"]'));
  const last = groups.last();
  await expect(last).toContainText('Unassigned');
  await expect(last.getByRole('img', { name: 'Unassigned' })).toBeVisible();
  await expect(last).toContainText('1 task');
});

test('GanttRowLabels_WhenSelected_ShouldMarkRowAndShowAccentBar', async ({ mount, page }) => {
  await mountChart(mount, FINISHED_TIMELINE, { selected: 'SHIP-192' });
  const r = page.getByTestId(ids.chartRow).and(page.locator('[data-key="SHIP-192"]'));
  await expect(r).toHaveAttribute('data-selected', 'true');
  await expect(r.getByTestId(ids.chartRowAccent)).toBeVisible();
  await expect(page.getByTestId(ids.chartRowAccent)).toHaveCount(1);
});

test('GanttRowLabels_WhenClicked_ShouldSelectOnlyBarAndStoryRows', async ({ mount, page }) => {
  const picks: string[] = [];
  await mountChart(mount, RUNNING_TIMELINE, { picks });
  const rows = page.getByTestId(ids.chartRow);
  await rows.and(page.locator('[data-key="SHIP-172"]')).click();
  await rows.and(page.locator('[data-key="SHIP-141"]')).click();
  await rows.and(page.locator('[data-key="SHIP-173"]')).click();
  await rows.and(page.locator('[data-key="SHIP-158"]')).click();
  expect(picks).toEqual(['SHIP-172', 'SHIP-141']);
});

test('GanttRowLabels_ShouldShowCriticalDotOnlyWithCriticalPathOn', async ({ mount, page }) => {
  const critical = page
    .getByTestId(ids.chartRow)
    .and(page.locator('[data-key="SHIP-191"]'))
    .getByTestId(ids.chartRowCp);
  const plain = page
    .getByTestId(ids.chartRow)
    .and(page.locator('[data-key="SHIP-193"]'))
    .getByTestId(ids.chartRowCp);
  const component = await mountChart(mount, FINISHED_TIMELINE);
  await expect(critical).toBeVisible();
  await expect(plain).toHaveCount(0);
  await component.update(
    <GanttChart
      view={FINISHED_TIMELINE}
      options={{ ...DEFAULT_TIMELINE_OPTIONS, cp: false }}
      selected={null}
      onSelect={() => undefined}
    />,
  );
  await expect(page.getByTestId(ids.chartRow).first()).toBeVisible();
  await expect(critical).toHaveCount(0);
});
