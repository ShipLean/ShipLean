import { expect, test } from '@playwright/experimental-ct-react';
import { touchTestIds } from '../../../touch/testIds';
import { InsightsAddressHarness } from '../ct/InsightsAddressHarness';
import { stubRetros } from '../ct/retroFixtures';
import { retrosTestIds as ids } from '../testIds';
import { CardBehindChip } from './ct/CardBehindChip';
import { insightsProject } from './ct/insightsProject';

// The Insights frame on a phone (SHIP-731, L731a): tabs, the Range sheet, ⋯ More and What these mean. Element shots of
// this task's regions only, so L731b-d's merges don't move them. Regenerate with
// scripts/update-page-shots.sh src/features/retros/insights/InsightsFrame.phone.spec.tsx.

const THEMES = ['light', 'dark'] as const;
const route = (query = '') => ({ hooksConfig: { route: `/p/SHIP/retros/insights${query}` } });
// Inside the page's 16px gutter (ProjectScreen's p-4), which the tabs bleed out of to the window's edges.
const inGutter = (
  <div className="p-4">
    <InsightsAddressHarness project={insightsProject} />
  </div>
);
const address = (page: Parameters<typeof stubRetros>[0]) => page.getByTestId('test-address');

for (const colour of THEMES) {
  test(`InsightsFrame_WhenRangeOpened_ShouldMatchItsScreenshot_retro-insights-range_${colour}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: colour, reducedMotion: 'reduce' });
    await stubRetros(page);
    await mount(inGutter, route('?sheet=range'));
    const sheet = page.getByTestId(touchTestIds.sheet);
    await expect(sheet).toBeVisible();
    await expect(sheet).toHaveScreenshot(`retro-insights-range.${colour}.png`, {
      animations: 'disabled',
    });
  });

  test(`InsightsFrame_WhenMoreOpened_ShouldMatchItsScreenshot_retro-insights-more_${colour}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: colour, reducedMotion: 'reduce' });
    await stubRetros(page);
    await mount(inGutter, route());
    await page.getByTestId(touchTestIds.moreButton).tap();
    const sheet = page.getByTestId(touchTestIds.sheet);
    await expect(sheet.getByTestId(touchTestIds.actionSheetRow)).toHaveCount(5);
    await expect(sheet.getByTestId(touchTestIds.actionSheetGroup)).toHaveText(['Go to']);
    await expect(sheet).toHaveScreenshot(`retro-insights-more.${colour}.png`, {
      animations: 'disabled',
    });
  });

  test(`InsightsFrame_WhenDefsOpened_ShouldMatchItsScreenshot_retro-insights-defs_${colour}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: colour, reducedMotion: 'reduce' });
    await stubRetros(page);
    await mount(inGutter, route('?sheet=defs:noEffect'));
    const sheet = page.getByTestId(touchTestIds.sheet);
    await expect(sheet.locator('[data-entry="noEffect"]')).toHaveAttribute(
      'data-highlighted',
      'true',
    );
    await expect(sheet).toHaveScreenshot(`retro-insights-defs.${colour}.png`, {
      animations: 'disabled',
    });
  });
}

test('InsightsFrame_WhenOpenedAt320_ShouldFitThreeEqualTabsAndKeepTheView', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 320, height: 700 });
  await stubRetros(page);
  await mount(inGutter, route());
  const tabs = page.getByTestId(ids.retroInsightsTab);
  await expect(tabs).toHaveText(['Themes', 'Action items', 'Impact']);
  const boxes = await tabs.evaluateAll((els) => els.map((e) => e.getBoundingClientRect()));
  expect(boxes[2].right).toBeLessThanOrEqual(320);
  expect(Math.round(boxes[0].width)).toBe(Math.round(boxes[2].width));
  expect(boxes[0].height).toBe(40);
  await tabs.nth(2).tap();
  await expect(address(page)).toHaveText('?view=impact');
});

test('InsightsFrame_WhenARangeIsPicked_ShouldWriteItAndCloseTheSheet', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(inGutter, route('?view=actions'));
  await page.getByTestId(ids.retroInsightsRange).tap();
  await expect(address(page)).toHaveText('?view=actions&sheet=range');
  const sheet = page.getByTestId(touchTestIds.sheet);
  await expect(sheet.getByRole('radiogroup')).toHaveCount(2);
  await sheet.getByTestId(ids.retroInsightsRangeOption).filter({ hasText: 'Last 12 weeks' }).tap();
  await expect(address(page)).toHaveText('?view=actions&range=w12');
  await expect(sheet).toHaveCount(0);
});

test('InsightsFrame_WhenAStatusChipIsTapped_ShouldOpenWhatTheseMeanAtItAndNotTheCardBehindIt', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(inGutter, route());
  const chip = page.getByTestId(ids.retroStatusChip).first();
  const status = await chip.getAttribute('data-status');
  await chip.tap();
  await expect(address(page)).toHaveText(`?sheet=defs%3A${status}`);
  await expect(page.getByTestId(ids.retroTipBubble)).toHaveCount(0);
  const sheet = page.getByTestId(touchTestIds.sheet);
  await expect(sheet.locator('[data-highlighted="true"]')).toHaveAttribute('data-entry', status!);
  await sheet.getByTestId(touchTestIds.sheetClose).tap();
  await expect(address(page)).toHaveText('');
});

test('InsightsFrame_WhenMoreIsUsed_ShouldWriteMergeAndDefsAndShowTheUnthemedCount', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(inGutter, route());
  await page.getByTestId(touchTestIds.moreButton).tap();
  const rows = page.getByTestId(touchTestIds.actionSheetRow);
  await expect(rows.nth(1)).toContainText('Give unthemed points themes');
  await expect(rows.nth(1).getByTestId(touchTestIds.actionSheetDetail)).toHaveText(/^\d+$/);
  await rows.nth(0).tap();
  await expect(address(page)).toHaveText('?merge=');
  // Merge themes is full screen now: closing it takes `merge` out of the address before ⋯ is reachable again.
  await page.getByTestId(touchTestIds.dialogClose).tap();
  await expect(address(page)).toHaveText('');
  await page.getByTestId(touchTestIds.moreButton).tap();
  await rows.nth(2).tap();
  await expect(address(page)).toHaveText('?sheet=defs');
});

test('InsightsFrame_WhenOpened_ShouldHaveNothingSticky', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(inGutter, route());
  await expect(page.getByTestId(ids.retroInsightsToolbar)).toBeVisible();
  const sticky = await page
    .getByTestId(ids.retroInsightsPanel)
    .evaluate(
      (panel) =>
        [...panel.querySelectorAll('*')].filter(
          (el) => getComputedStyle(el).position === 'sticky' && el.getClientRects().length > 0,
        ).length,
    );
  expect(sticky).toBe(0);
});

test('InsightsFrame_WhenAChipInsideACardIsTapped_ShouldNotOpenTheCard', async ({ mount, page }) => {
  await mount(<CardBehindChip />, route());
  await page.getByTestId(ids.retroStatusChip).tap();
  await expect(page.getByTestId('card')).toHaveAttribute('data-opened', 'false');
  await expect(page.getByTestId('card')).toBeVisible();
});

test('InsightsMore_ShouldShowAGroupHeading', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(inGutter, route());
  await page.getByTestId(touchTestIds.moreButton).tap();
  const sheet = page.getByTestId(touchTestIds.sheet);
  await expect(sheet.getByTestId(touchTestIds.actionSheetRow)).toHaveCount(5);
  const heading = sheet.getByTestId(touchTestIds.actionSheetGroup);
  await expect(heading).toHaveText('Go to');
  // K1 and the design: Merge themes, Give unthemed and What the statuses mean are ungrouped; the heading sits above the two navigation rows.
  const rows = await sheet
    .getByTestId(touchTestIds.actionSheetRow)
    .evaluateAll((els) => els.map((e) => e.getBoundingClientRect().top));
  const headingTop = (await heading.boundingBox())!.y;
  expect(rows.filter((top) => top < headingTop)).toHaveLength(3);
  // Merge's text is the right-aligned detail, not a sub-line.
  await expect(sheet.getByTestId(touchTestIds.actionSheetHint)).toHaveCount(0);
  await expect(
    sheet
      .getByTestId(touchTestIds.actionSheetRow)
      .first()
      .getByTestId(touchTestIds.actionSheetDetail),
  ).toHaveText('fold one theme into another');
  // The defs row opens another sheet: it carries the › like the navigation rows.
  await expect(
    sheet.getByTestId(touchTestIds.actionSheetRow).filter({ hasText: 'What the statuses' }),
  ).toContainText('›');
});

test('InsightsFrame_WhenOpened_ShouldHaveTabsFullBleedAndABorderedMore', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(inGutter, route());
  const tabs = page.getByTestId(ids.retroInsightsTab);
  await expect(tabs).toHaveCount(3);
  const first = (await tabs.first().boundingBox())!;
  const last = (await tabs.last().boundingBox())!;
  // Flush with the window's left edge and top, and the right edge is the document's client width (a classic
  // scrollbar would make innerWidth wider than that).
  const clientWidth = await page.evaluate(() => document.documentElement.clientWidth);
  expect(first.x).toBe(0);
  expect(first.y).toBe(0);
  expect(last.x + last.width).toBe(clientWidth);
  const toolbar = page.getByTestId(ids.retroInsightsToolbar);
  const fits = await toolbar.evaluate((el) => el.scrollWidth <= el.clientWidth);
  expect(fits).toBe(true);
  const more = page.getByTestId(touchTestIds.moreButton);
  await expect(more).toHaveCSS('border-top-width', '1px');
});
