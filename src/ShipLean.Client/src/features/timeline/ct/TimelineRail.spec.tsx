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
  await expect(page.getByTestId(ids.railNotStarted)).toHaveCount(0);
  await expect(page.getByTestId(ids.railSelected)).toHaveCount(0);
  await expect(page.getByTestId(ids.railGlyph)).toHaveCount(6);
  await expect(page.getByTestId(ids.railHint)).toHaveText(
    "Click a bar, a row or a review to see that item's history.",
  );
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
  await expect(list.getByTestId(ids.typeBadge)).toHaveCount(2);
  await expect(list.getByText('SHIP-158', { exact: true })).toBeVisible();
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

test('TimelineRail_WhenTaskSelected_ShouldShowTheStructuredCard', async ({ mount, page }) => {
  const selections: (string | null)[] = [];
  await mount(
    <TimelineRail
      view={FINISHED_TIMELINE}
      options={DEFAULT_TIMELINE_OPTIONS}
      selected="SHIP-192"
      onSelect={(key) => selections.push(key)}
    />,
  );
  const panel = page.getByTestId(ids.railSelected);
  await expect(
    panel.getByRole('heading', { name: 'Stacking rules (percent + fixed)' }),
  ).toBeVisible();
  const facts = page.getByTestId(ids.railFacts);
  await expect(facts).toContainText('AssigneeClaude Code');
  await expect(page.getByTestId(ids.railAgentChip)).toHaveText('Agent');
  await expect(facts).toContainText('ActiveTue 6 Oct, 10:30');
  await expect(facts).toContainText('ResolvedWed 7 Oct, 11:15');
  await expect(facts).toContainText('Took1 day');
  await expect(facts).toContainText('ReviewedWed 7 Oct, 09:30');
  await expect(page.getByTestId(ids.railCriticalPill)).toHaveText('Critical path');
  await expect(panel.getByRole('button', { name: 'SHIP-185' })).toBeVisible();
  await expect(page.getByTestId(ids.railHistoryRow)).toHaveCount(4);
  await expect(page.getByTestId(ids.railHistoryRow).first().locator('[data-duration]')).toHaveText(
    '3.5 h',
  );
  await expect(page.getByTestId(ids.railReviewRow)).toHaveCount(2);
  await expect(page.getByTestId(ids.railReviewRow).first()).toContainText('Changes requested');
  await expect(page.getByTestId(ids.railReviewRow).first()).toContainText('wrong order');
  await expect(page.getByTestId(ids.railReviewRow).last()).toContainText('Approved');
  await expect(page.getByRole('link', { name: 'Open SHIP-192' })).toHaveAttribute(
    'href',
    '/p/SHIP/items/SHIP-192',
  );
  await panel.getByRole('button', { name: 'SHIP-185' }).click();
  await page.getByRole('button', { name: '← Reading the chart' }).click();
  expect(selections).toEqual(['SHIP-185', null]);
});

test('TimelineRail_WhenTaskHasLastCommit_ShouldShowItsLabel', async ({ mount, page }) => {
  await mount(
    <TimelineRail
      view={FINISHED_TIMELINE}
      options={DEFAULT_TIMELINE_OPTIONS}
      selected="SHIP-192"
      onSelect={noop}
    />,
  );
  await expect(page.getByTestId(ids.railSelected)).toContainText('Last commitWed 7 Oct, 11:05');
});

test('TimelineRail_WhenTaskHasNoCommitTime_ShouldNotShowLastCommit', async ({ mount, page }) => {
  await mount(
    <TimelineRail
      view={FINISHED_TIMELINE}
      options={DEFAULT_TIMELINE_OPTIONS}
      selected="SHIP-193"
      onSelect={noop}
    />,
  );
  await expect(page.getByTestId(ids.railSelected)).toContainText('History');
  await expect(page.getByTestId(ids.railSelected)).not.toContainText('Last commit');
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
  const facts = page.getByTestId(ids.railFacts);
  await expect(facts).toContainText('ActiveNot recorded');
  await expect(facts).toContainText('Took—');
  await expect(page.getByTestId(ids.railNoStart)).toHaveText(
    'Closed in one bulk edit, so no start was recorded; it shows as a marker.',
  );
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
