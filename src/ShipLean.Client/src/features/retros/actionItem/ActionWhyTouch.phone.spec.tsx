import { expect, test } from '@playwright/experimental-ct-react';
import { story } from '../../workItem/ct/fixtures';
import { stubApi } from '../../workItem/ct/stubApi';
import { workItemTestIds as workItemIds } from '../../workItem/testIds';
import { retroActionDetail, stubRetros } from '../ct/retroFixtures';
import { WorkItemRetroHarness } from '../workItem/ct/WorkItemRetroHarness';
import { retrosTestIds as ids } from '../testIds';

// An action item's page on the CT phone project (390×844, SHIP-714 D2/D9, SHIP-805 rule 13, SHIP-1626 SHELL-5).

const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-141' } };
const action = {
  ...story,
  type: { id: 'action' as const, name: 'Action item' },
  parent: null,
  acceptanceCriteria: [],
};

for (const theme of ['light', 'dark'] as const) {
  test(`ActionItem_WhenOpened_ShouldMatchItsScreenshot_why-chips_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubApi(page, action);
    await stubRetros(page);
    const screen = await mount(<WorkItemRetroHarness itemKey="SHIP-141" />, route);
    const chips = page.getByTestId(workItemIds.sectionChips);
    await expect(
      chips.getByTestId(workItemIds.sectionChip).filter({ hasText: /^Why/ }),
    ).toBeVisible();
    await expect(screen.getByTestId(ids.retroActionWhy)).toBeVisible();

    await expect(chips).toHaveScreenshot(`why-chips.${theme}.png`, { animations: 'disabled' });
  });
}

test('ActionItem_ShouldOfferTheWhySectionChip', async ({ mount, page }) => {
  await stubApi(page, action);
  await stubRetros(page);
  const screen = await mount(<WorkItemRetroHarness itemKey="SHIP-141" />, route);
  await expect(screen.getByTestId(ids.retroActionWhy)).toBeVisible();

  const chip = page.getByTestId(workItemIds.sectionChip).filter({ hasText: /^Why/ });
  await expect(chip).toHaveText(`Why${retroActionDetail.points.length}`);
  const box = (await chip.boundingBox())!;
  expect(box.height).toBeGreaterThanOrEqual(32);

  const add = (await screen.getByTestId(ids.retroActionWhyAdd).boundingBox())!;
  expect(add.height).toBeGreaterThanOrEqual(32);
});
