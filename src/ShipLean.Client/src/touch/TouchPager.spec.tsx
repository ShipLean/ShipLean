import { expect, test } from '@playwright/experimental-ct-react';
import { PagerHarness } from './ct/PagerHarness';
import { touchTestIds } from './testIds';

// Desktop project, used only to load a tablet width.
test('TouchPager_WhenOnATablet_ShouldShowTwoAndABitPages', async ({ mount, page }) => {
  await page.setViewportSize({ width: 768, height: 1024 });
  await mount(<PagerHarness />);
  const pages = page.getByTestId(touchTestIds.pagerPage);
  const first = await pages.nth(0).boundingBox();
  const second = await pages.nth(1).boundingBox();
  const third = await pages.nth(2).boundingBox();
  expect(first?.width).toBe(340);
  // Two whole pages, then the start of a third.
  expect((second?.x ?? 0) + (second?.width ?? 0)).toBeLessThanOrEqual(768);
  expect(third?.x).toBeLessThan(768);
  expect((third?.x ?? 0) + (third?.width ?? 0)).toBeGreaterThan(768);
});
