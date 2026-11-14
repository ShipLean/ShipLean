import { expect, test } from '@playwright/experimental-ct-react';
import { DEFAULT_TIMELINE_OPTIONS } from '../TimelineOptions';
import { timelineTestIds as ids } from '../testIds';
import { TimelineRail } from '../TimelineRail';
import { FINISHED_TIMELINE, RUNNING_TIMELINE } from './stubFeatureTimeline';

const noop = () => undefined;

test('TimelineRail_WhenNothingSelected_ShouldShowOverview', async ({ mount, page }) => {
  await mount(
    <TimelineRail
      view={FINISHED_TIMELINE}
      options={DEFAULT_TIMELINE_OPTIONS}
      selected={null}
      onSelect={noop}
    />,
  );
  await expect(page.getByTestId(ids.railOverview)).toContainText('Reading the chart');
  await expect(page.getByTestId(ids.railOverview)).toContainText('Agent run · later');
  await expect(page.getByRole('button', { name: 'Agent run · later' })).toBeDisabled();
  await expect(page.getByTestId(ids.railNotStarted)).toHaveCount(0);
  await expect(page.getByTestId(ids.railSelected)).toHaveCount(0);
});

test('TimelineRail_WhenRunning_ShouldListNotStarted', async ({ mount, page }) => {
  await mount(
    <TimelineRail
      view={RUNNING_TIMELINE}
      options={DEFAULT_TIMELINE_OPTIONS}
      selected={null}
      onSelect={noop}
    />,
  );
  const list = page.getByTestId(ids.railNotStarted);
  await expect(list).toContainText('SHIP-158');
  await expect(list).toContainText('Gift card balance check');
  await expect(list).toContainText('SHIP-173');
  await expect(list.getByRole('link', { name: 'SHIP-173' })).toHaveAttribute(
    'href',
    '/p/SHIP/items/SHIP-173',
  );
});

test('TimelineRail_WhenSelectedKeyUnknown_ShouldShowOverview', async ({ mount, page }) => {
  await mount(
    <TimelineRail
      view={FINISHED_TIMELINE}
      options={DEFAULT_TIMELINE_OPTIONS}
      selected="SHIP-9999"
      onSelect={noop}
    />,
  );
  await expect(page.getByTestId(ids.railOverview)).toBeVisible();
  await expect(page.getByTestId(ids.railSelected)).toHaveCount(0);
});

test('TimelineRail_WhenTaskSelected_ShouldShowHistoryAndReviews', async ({ mount, page }) => {
  await mount(
    <TimelineRail
      view={FINISHED_TIMELINE}
      options={DEFAULT_TIMELINE_OPTIONS}
      selected="SHIP-192"
      onSelect={noop}
    />,
  );
  const panel = page.getByTestId(ids.railSelected);
  await expect(panel).toContainText('Stacking rules (percent + fixed)');
  await expect(panel).toContainText('Claude Code (agent)');
  await expect(panel).toContainText('On the critical path');
  await expect(panel.getByRole('link', { name: 'SHIP-192' })).toHaveAttribute(
    'href',
    '/p/SHIP/items/SHIP-192',
  );
  await expect(page.getByTestId(ids.railHistoryRow)).toHaveCount(4);
  await expect(page.getByTestId(ids.railReviewRow)).toHaveCount(2);
  await expect(page.getByTestId(ids.railReviewRow).first()).toContainText('Changes requested');
  await expect(page.getByTestId(ids.railReviewRow).first()).toContainText('wrong order');
  await expect(page.getByTestId(ids.railReviewRow).last()).toContainText('Approved');
});

test('TimelineRail_WhenBlockedPeriod_ShouldShowBlockedWithoutReason', async ({ mount, page }) => {
  await mount(
    <TimelineRail
      view={FINISHED_TIMELINE}
      options={DEFAULT_TIMELINE_OPTIONS}
      selected="SHIP-194"
      onSelect={noop}
    />,
  );
  const blocked = page.locator(`[data-testid="${ids.railHistoryRow}"][data-state="blocked"]`);
  await expect(blocked).toHaveCount(1);
  await expect(blocked).toHaveText('Blocked · Thu 8 Oct, 15:00 · 3.8 days');
});

test('TimelineRail_WhenWaitedOnClicked_ShouldSelectThatItem', async ({ mount, page }) => {
  const selections: (string | null)[] = [];
  await mount(
    <TimelineRail
      view={FINISHED_TIMELINE}
      options={DEFAULT_TIMELINE_OPTIONS}
      selected="SHIP-192"
      onSelect={(key) => selections.push(key)}
    />,
  );
  const waited = page.getByTestId(ids.railWaitedOn);
  await expect(waited).toContainText('22.8 h');
  await waited.getByRole('button', { name: 'SHIP-191' }).click();
  expect(selections).toEqual(['SHIP-191']);
});

test('TimelineRail_WhenHeldUpClicked_ShouldSelectThatItem', async ({ mount, page }) => {
  const selections: (string | null)[] = [];
  await mount(
    <TimelineRail
      view={FINISHED_TIMELINE}
      options={DEFAULT_TIMELINE_OPTIONS}
      selected="SHIP-192"
      onSelect={(key) => selections.push(key)}
    />,
  );
  await page.getByTestId(ids.railHeldUp).getByRole('button', { name: 'SHIP-194' }).click();
  expect(selections).toEqual(['SHIP-194']);
});

test('TimelineRail_WhenInferred_ShouldSayClosedNoStartRecorded', async ({ mount, page }) => {
  await mount(
    <TimelineRail
      view={FINISHED_TIMELINE}
      options={DEFAULT_TIMELINE_OPTIONS}
      selected="SHIP-198"
      onSelect={noop}
    />,
  );
  await expect(page.getByTestId(ids.railSelected)).toContainText('Closed, no start recorded');
});

test('TimelineRail_WhenStorySelected_ShouldListItsTasks', async ({ mount, page }) => {
  const selections: (string | null)[] = [];
  await mount(
    <TimelineRail
      view={FINISHED_TIMELINE}
      options={DEFAULT_TIMELINE_OPTIONS}
      selected="SHIP-185"
      onSelect={(key) => selections.push(key)}
    />,
  );
  const panel = page.getByTestId(ids.railSelected);
  await expect(panel).toContainText('Coupon rules engine');
  await expect(panel).toContainText('2.1 days');
  for (const key of ['SHIP-191', 'SHIP-192', 'SHIP-193'])
    await expect(panel.getByRole('button', { name: key })).toBeVisible();
  await panel.getByRole('button', { name: 'SHIP-193' }).click();
  expect(selections).toEqual(['SHIP-193']);
});

test('TimelineRail_WhenCloseClicked_ShouldClearSelection', async ({ mount, page }) => {
  const selections: (string | null)[] = [];
  await mount(
    <TimelineRail
      view={FINISHED_TIMELINE}
      options={DEFAULT_TIMELINE_OPTIONS}
      selected="SHIP-192"
      onSelect={(key) => selections.push(key)}
    />,
  );
  await page.getByTestId(ids.railClose).click();
  expect(selections).toEqual([null]);
});

test('TimelineRail_WhenJsonView_ShouldShowFieldGuide', async ({ mount, page }) => {
  await mount(
    <TimelineRail
      view={FINISHED_TIMELINE}
      options={{ ...DEFAULT_TIMELINE_OPTIONS, view: 'json' }}
      selected={null}
      onSelect={noop}
    />,
  );
  const guide = page.getByTestId(ids.railOverview);
  for (const name of ['bars', 'edges', 'critical_path', 'key_dates'])
    await expect(guide).toContainText(name);
  await expect(page.getByTestId(ids.rail)).toHaveAttribute('data-view', 'json');
});

for (const scheme of ['light', 'dark'] as const) {
  test(`TimelineRail_Selected_ShouldMatchScreenshot_${scheme}`, async ({ mount, page }) => {
    await page.emulateMedia({ colorScheme: scheme });
    await mount(
      <TimelineRail
        view={FINISHED_TIMELINE}
        options={DEFAULT_TIMELINE_OPTIONS}
        selected="SHIP-192"
        onSelect={noop}
      />,
    );
    await expect(page.getByTestId(ids.rail)).toHaveScreenshot(
      `timeline-rail-selected-${scheme}.png`,
    );
  });
}
