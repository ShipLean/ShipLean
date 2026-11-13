import { expect, test } from '@playwright/experimental-ct-react';
import { bug, story } from '../../features/workItem/ct/fixtures';
import { stubApi } from '../../features/workItem/ct/stubApi';
import { WorkItemFormHarness } from '../../features/workItem/ct/WorkItemFormHarness';
import { workItemTestIds as ids } from '../../features/workItem/testIds';
import { touchTestIds } from '../../touch/testIds';
import type { Page } from './shoot';

// The work item's fields and pickers on a phone (SHIP-714, L714b), as element shots of this task's regions only, so the
// other 714 tasks' merges don't move them: the fields block, the State and Parent sheets, and a Bug's repro block.
// Regenerate with scripts/update-page-shots.sh src/pages/screenshots/itemFields.phone.spec.tsx.

const THEMES = ['light', 'dark'] as const;
const route = (key: string) => ({ hooksConfig: { route: `/p/SHIP/items/${key}` } });

const epic = {
  id: 'e1',
  key: 'SHIP-90',
  type: { id: 'epic' as const, name: 'Epic' },
  title: 'Checkout conversion',
};
const checkout = {
  ...story,
  id: 'f101',
  key: 'SHIP-101',
  type: { id: 'feature' as const, name: 'Feature' },
  title: 'Checkout v2',
  parent: epic,
};
const reporting = { ...checkout, id: 'f103', key: 'SHIP-103', title: 'Reporting', parent: null };
const parented = {
  ...story,
  parent: { id: checkout.id, key: checkout.key, type: checkout.type, title: checkout.title },
  tags: ['Checkout', 'PCI'],
};

const phoneOnly = (page: Page & { viewportSize(): { width: number } | null }) =>
  expect(page.viewportSize()?.width, 'phone shots run only in the CT phone project').toBe(390);

for (const theme of THEMES) {
  test(`ItemFieldsPhone_WhenOpened_ShouldMatchItsScreenshot_item-fields_${theme}`, async ({
    mount,
    page,
  }) => {
    phoneOnly(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubApi(page, parented);
    const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route('SHIP-141'));
    await expect(screen.getByTestId(ids.sprint)).toHaveValue('sp24');
    await expect(screen.getByTestId(ids.assigneeButton)).toContainText('Priya');

    await expect(screen.getByTestId(ids.fields)).toHaveScreenshot(`item-fields.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`ItemFieldsPhone_WhenStateOpened_ShouldMatchItsScreenshot_item-state-sheet_${theme}`, async ({
    mount,
    page,
  }) => {
    phoneOnly(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubApi(page, story);
    const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route('SHIP-141'));
    await screen.getByTestId(ids.stateButton).tap();
    const sheet = page.getByTestId(touchTestIds.sheet);
    await expect(sheet.getByTestId(touchTestIds.sheetTitle)).toHaveText('State');

    await expect(sheet).toHaveScreenshot(`item-state-sheet.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`ItemFieldsPhone_WhenParentOpened_ShouldMatchItsScreenshot_item-parent-sheet_${theme}`, async ({
    mount,
    page,
  }) => {
    phoneOnly(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubApi(page, parented, undefined, { byType: { feature: [checkout, reporting] } });
    const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route('SHIP-141'));
    await screen.getByTestId(ids.parentButton).tap();
    const sheet = page.getByTestId(touchTestIds.sheet);
    await expect(sheet.getByTestId(ids.parentOption)).toHaveCount(3);

    await expect(sheet).toHaveScreenshot(`item-parent-sheet.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`ItemFieldsPhone_WhenBug_ShouldMatchItsScreenshot_item-bug-repro_${theme}`, async ({
    mount,
    page,
  }) => {
    phoneOnly(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubApi(page, bug);
    const screen = await mount(<WorkItemFormHarness itemKey="SHIP-153" />, route('SHIP-153'));
    await expect(screen.getByTestId(ids.severity)).toHaveValue('2');

    await expect(screen.getByTestId(ids.reproBlock)).toHaveScreenshot(
      `item-bug-repro.${theme}.png`,
      { animations: 'disabled' },
    );
  });
}
