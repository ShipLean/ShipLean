import { expect, test } from '@playwright/experimental-ct-react';
import { feature } from '../../feature/ct/stubFeature';
import type { FeatureRegionProps } from '../../feature/FeatureRegionProps';
import { featureTestIds } from '../../feature/testIds';
import { FeatureTimelineSection } from '../FeatureTimelineSection';
import { timelineTestIds as ids } from '../testIds';
import { stubFeatureTimeline } from './stubFeatureTimeline';

const region = {
  item: feature,
  draft: {},
  dirty: false,
  changed: [],
  setField: () => undefined,
  saveError: null,
  projectKey: 'SHIP',
} as unknown as FeatureRegionProps;
const mountSection = (mount: Parameters<Parameters<typeof test>[2]>[0]['mount']) =>
  mount(<FeatureTimelineSection {...region} />);

test('FeatureTimelineSection_ShouldLinkToTimelineWithFromFeature', async ({ mount, page }) => {
  await stubFeatureTimeline(page, { fixture: 'finished' });
  await mountSection(mount);
  await expect(page.getByTestId(ids.keyDates)).toBeVisible();
  await expect(page.getByRole('link', { name: 'Open timeline' })).toHaveAttribute(
    'href',
    `/p/SHIP/items/${feature.key}/timeline?from=feature`,
  );
});

test('FeatureTimelineSection_WhenNarrow_ShouldHideOpenTimeline', async ({ mount, page }) => {
  await stubFeatureTimeline(page, { fixture: 'finished' });
  await page.setViewportSize({ width: 800, height: 600 });
  await mountSection(mount);
  await expect(page.getByTestId(ids.keyDates)).toBeVisible();
  await expect(page.getByRole('link', { name: 'Open timeline' })).toBeHidden();
});

test('FeatureTimelineSection_WhenWide_ShouldShowIt', async ({ mount, page }) => {
  await stubFeatureTimeline(page, { fixture: 'finished' });
  await page.setViewportSize({ width: 1280, height: 800 });
  await mountSection(mount);
  await expect(page.getByRole('link', { name: 'Open timeline' })).toBeVisible();
});

test('FeatureTimelineSection_WhenLoading_ShouldShowSkeleton', async ({ mount, page }) => {
  await page.route('**/api/projects/*/work-items/by-key/*/timeline', () => new Promise(() => {}));
  await mountSection(mount);
  await expect(page.getByTestId(ids.loading)).toBeVisible();
  await expect(page.getByTestId(ids.keyDates)).toHaveCount(0);
});

test('FeatureTimelineSection_WhenError_ShouldShowOneLineAndKeepThePage', async ({
  mount,
  page,
}) => {
  await stubFeatureTimeline(page, { fixture: 'error' });
  await mountSection(mount);
  await expect(page.getByTestId(ids.loadFailed)).toBeVisible();
  await expect(page.getByTestId(featureTestIds.featureTimelineSection)).toContainText('Timeline');
  await expect(page.getByTestId(ids.keyDates)).toHaveCount(0);
});

for (const scheme of ['light', 'dark'] as const) {
  test(`FeatureTimelineSection_ShouldLookRight_${scheme}`, async ({ mount, page }) => {
    await stubFeatureTimeline(page, { fixture: 'finished' });
    await page.emulateMedia({ colorScheme: scheme });
    await page.setViewportSize({ width: 1280, height: 800 });
    const screen = await mountSection(mount);
    await expect(page.getByTestId(ids.keyDates)).toBeVisible();
    await expect(screen).toHaveScreenshot(`feature-timeline-section-${scheme}.png`);
  });
}
