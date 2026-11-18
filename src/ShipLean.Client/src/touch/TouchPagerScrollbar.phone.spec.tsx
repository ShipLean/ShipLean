import { expect, test } from '@playwright/experimental-ct-react';
import { PagerHarness } from './ct/PagerHarness';
import { touchTestIds } from './testIds';

// The pager's bleed (K5) with a classic scrollbar, as a desktop browser narrowed to the touch layout draws one. The phone
// project hides scrollbars (headless --hide-scrollbars, overlay scrollbars on a mobile device), so this file turns both
// off: a styled ::-webkit-scrollbar then takes 15px, and the window's content area is narrower than 100vw.
test.use({ isMobile: false, launchOptions: { ignoreDefaultArgs: ['--hide-scrollbars'] } });

test('TouchPager_WhenTheWindowHasAScrollbar_ShouldBleedOnlyToItsEdge', async ({ mount, page }) => {
  await page.addStyleTag({
    content:
      'html::-webkit-scrollbar { width: 15px; background: #ccc } body { min-height: 3000px }',
  });
  await mount(<PagerHarness />);
  const width = await page.evaluate(() => document.documentElement.clientWidth);
  expect(width).toBe(375);
  await expect
    .poll(async () => {
      const box = (await page.getByTestId(touchTestIds.pagerChips).boundingBox())!;
      return [box.x, box.x + box.width];
    })
    .toEqual([0, width]);
  const sideways = await page.evaluate(
    () => document.documentElement.scrollWidth > document.documentElement.clientWidth,
  );
  expect(sideways).toBe(false);
});
