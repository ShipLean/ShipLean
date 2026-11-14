import { expect, test } from '@playwright/experimental-ct-react';
import { KeyDates } from '../KeyDates';
import { timelineTestIds as ids } from '../testIds';
import { FINISHED_TIMELINE, RUNNING_TIMELINE } from './stubFeatureTimeline';

test('KeyDates_ShouldShowFourPointsAndThreeGaps', async ({ mount, page }) => {
  await mount(<KeyDates view={FINISHED_TIMELINE} compact={false} />);
  const points = page.getByTestId(ids.keyDatePoint);
  await expect(points).toHaveCount(4);
  await expect(points.nth(0)).toContainText('Added');
  await expect(points.nth(0)).toContainText('Tue 18 Aug');
  await expect(points.nth(0)).toContainText('Feature created · Ana Costa');
  await expect(points.nth(3)).toContainText('Finished');
  const gaps = page.getByTestId(ids.keyDateGap);
  await expect(gaps).toHaveCount(3);
  await expect(gaps.nth(0)).toHaveText('44 days');
  await expect(gaps.nth(1)).toHaveText('3.7 days');
  await expect(gaps.nth(2)).toHaveText('16 days');
});

test('KeyDates_WhenRunning_ShouldSayNotYetAndSoFar', async ({ mount, page }) => {
  await mount(<KeyDates view={RUNNING_TIMELINE} compact={false} />);
  const finished = page.getByTestId(ids.keyDatePoint).nth(3);
  await expect(finished).toContainText('Not yet');
  await expect(finished).toContainText('32 days so far');
  await expect(page.getByTestId(ids.keyDateGap).nth(2)).toHaveText('32 days so far');
  await expect(page.getByTestId(ids.keyDateGap).nth(0)).toHaveText('6 hours');
});

test('KeyDates_WhenCompact_ShouldShowNameOnly', async ({ mount, page }) => {
  await mount(<KeyDates view={FINISHED_TIMELINE} compact />);
  const added = page.getByTestId(ids.keyDatePoint).nth(0);
  await expect(added).toContainText('Ana Costa');
  await expect(added).not.toContainText('Feature created');
  await expect(page.getByTestId(ids.keyDates)).toHaveAttribute('data-compact', 'true');
});

for (const scheme of ['light', 'dark'] as const) {
  test(`KeyDates_ShouldLookRight_${scheme}`, async ({ mount, page }) => {
    await page.emulateMedia({ colorScheme: scheme });
    const screen = await mount(<KeyDates view={FINISHED_TIMELINE} compact={false} />);
    await expect(screen).toHaveScreenshot(`key-dates-${scheme}.png`);
  });
}
