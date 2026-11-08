import { expect, test } from '@playwright/experimental-ct-react';
import { PagerHarness } from './ct/PagerHarness';
import { pagerHarnessIds as ids } from './ct/pagerHarnessIds';
import { touchTestIds } from './testIds';

type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];

const pageById = (page: Page, id: string) =>
  page.locator(`[data-testid="${touchTestIds.pagerPage}"][data-page-id="${id}"]`);
const chipById = (page: Page, id: string) =>
  page.locator(`[data-testid="${touchTestIds.pagerChip}"][data-page-id="${id}"]`);

// A swipe with real touch input, so the browser's own scrolling and snapping do the work.
async function swipeLeft(page: Page, from: { x: number; y: number }, distance: number) {
  const client = await page.context().newCDPSession(page);
  await client.send('Input.dispatchTouchEvent', {
    type: 'touchStart',
    touchPoints: [{ x: from.x, y: from.y }],
  });
  const steps = 12;
  for (let i = 1; i <= steps; i++) {
    await client.send('Input.dispatchTouchEvent', {
      type: 'touchMove',
      touchPoints: [{ x: from.x - (distance * i) / steps, y: from.y }],
    });
    await page.evaluate(() => new Promise((done) => requestAnimationFrame(done)));
  }
  await client.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
}

test('TouchPager_WhenOnAPhone_ShouldShowOnePageWithTheNextPeeking', async ({ mount, page }) => {
  await mount(<PagerHarness />);
  const first = await pageById(page, 'todo').boundingBox();
  const second = await pageById(page, 'doing').boundingBox();
  expect(first?.width).toBe(340);
  expect(second?.x).toBeLessThan(390);
  expect(second?.x).toBeGreaterThan(340);
});

test('TouchPager_WhenChipTapped_ShouldTurnToItsPage', async ({ mount, page }) => {
  await mount(<PagerHarness />);
  await chipById(page, 'review').click();
  await expect(chipById(page, 'review')).toHaveAttribute('aria-current', 'true');
  await expect
    .poll(async () => Math.round((await pageById(page, 'review').boundingBox())?.x ?? -1))
    .toBe(12);
  await expect(page.getByTestId(ids.ctPagerCurrent)).toHaveText('review');
});

test('TouchPager_WhenSwipedToTheNextPage_ShouldHighlightItsChipAndReportIt', async ({
  mount,
  page,
}) => {
  await mount(<PagerHarness />);
  await swipeLeft(page, { x: 300, y: 400 }, 200);
  await expect(chipById(page, 'doing')).toHaveAttribute('aria-current', 'true');
  await expect(chipById(page, 'todo')).not.toHaveAttribute('aria-current', 'true');
  await expect(page.getByTestId(ids.ctPagerReports)).toHaveText('doing');
});

test('TouchPager_WhenOverTheLimit_ShouldMarkTheChipDanger', async ({ mount, page }) => {
  await mount(<PagerHarness />);
  const over = chipById(page, 'doing');
  await expect(over).toHaveAttribute('data-over', 'true');
  await expect(over).toContainText('4/3');
  await expect(chipById(page, 'todo')).toHaveAttribute('data-over', 'false');
  const color = await over
    .locator('.touch-pager-chip-count')
    .evaluate((el) => getComputedStyle(el).color);
  const danger = await page.evaluate(() => {
    const probe = document.createElement('span');
    probe.style.color = 'var(--color-danger)';
    document.body.append(probe);
    const value = getComputedStyle(probe).color;
    probe.remove();
    return value;
  });
  expect(color).toBe(danger);
});

test('TouchPager_WhenAPageIsInsertedBeforeTheCurrentOne_ShouldStayOnTheCurrentPage', async ({
  mount,
  page,
}) => {
  await mount(<PagerHarness />);
  await chipById(page, 'doing').click();
  await expect
    .poll(async () => Math.round((await pageById(page, 'doing').boundingBox())?.x ?? -1))
    .toBe(12);
  await page.getByTestId(ids.ctPagerInsert).click();
  await expect(pageById(page, 'new-0')).toHaveCount(1);
  await expect
    .poll(async () => Math.round((await pageById(page, 'doing').boundingBox())?.x ?? -1))
    .toBe(12);
  await expect(chipById(page, 'doing')).toHaveAttribute('aria-current', 'true');
  await expect(page.getByTestId(ids.ctPagerCurrent)).toHaveText('doing');
});

test('TouchPager_WhenCurrentChangesFromOutside_ShouldTurnToIt', async ({ mount, page }) => {
  await mount(<PagerHarness />);
  await page.getByTestId(ids.ctPagerGoToLast).click();
  await expect(chipById(page, 'done')).toHaveAttribute('aria-current', 'true');
  const right = async () =>
    Math.round(((await pageById(page, 'done').boundingBox())?.x ?? -1) + 340);
  await expect.poll(right).toBeLessThanOrEqual(390);
  await expect(page.getByTestId(ids.ctPagerReports)).toHaveText('');
});

test('TouchPager_WhenPulseChanges_ShouldPulseThatChipAndStayOnThePage', async ({ mount, page }) => {
  await mount(<PagerHarness />);
  await expect(chipById(page, 'review')).not.toHaveClass(/is-pulsing/);
  await page.getByTestId(ids.ctPagerPulse).click();
  await expect(chipById(page, 'review')).toHaveClass(/is-pulsing/);
  await expect(chipById(page, 'todo')).not.toHaveClass(/is-pulsing/);
  await expect(chipById(page, 'todo')).toHaveAttribute('aria-current', 'true');
  await expect(chipById(page, 'review')).not.toHaveClass(/is-pulsing/);
  await expect(page.getByTestId(ids.ctPagerCurrent)).toHaveText('todo');
  expect((await pageById(page, 'todo').boundingBox())?.x).toBe(12);
});

// On a mobile-emulated page `innerWidth` grows to fit overflowing content, so `scrollWidth <= innerWidth` can't fail;
// the document's `clientWidth` stays at the device width, so it is checked too.
const fitsTheWidth = (page: Page) =>
  page.evaluate(() => {
    const root = document.documentElement;
    return root.scrollWidth <= window.innerWidth && root.scrollWidth <= root.clientWidth;
  });

test('TouchPager_WhenOpen_ShouldNotScrollThePageSideways', async ({ mount, page }) => {
  await mount(<PagerHarness />);
  expect(await fitsTheWidth(page)).toBe(true);
  await chipById(page, 'done').click();
  await expect(chipById(page, 'done')).toHaveAttribute('aria-current', 'true');
  expect(await fitsTheWidth(page)).toBe(true);
});

for (const theme of ['light', 'dark'] as const) {
  test(`TouchPager_WhenRendered_ShouldMatchItsScreenshot_${theme}`, async ({ mount, page }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    const component = await mount(<PagerHarness />);
    await expect(chipById(page, 'todo')).toHaveAttribute('aria-current', 'true');
    await expect(component).toHaveScreenshot(`touch-pager.${theme}.png`, {
      animations: 'disabled',
    });
  });
}
