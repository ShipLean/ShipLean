import { expect, test } from '@playwright/experimental-ct-react';
import { FeatureBoardSlot } from '../../../slots/FeatureBoardSlot';
import { touchTestIds } from '../../../touch/testIds';
import { FEATURE_KEY, FEATURE_VIEW } from '../../featureBoard/ct/fixtures';
import { stubFeatureBoard } from '../../featureBoard/ct/stubFeatureBoard';
import { taskboardTestIds as ids } from '../testIds';
import { PROJECT } from './fixtures';

// The shared Taskboard toolbar on a phone (SHIP-722 D5, D6): one row, the Filter sheet and ⋯ More. Runs in the CT phone
// project, through the Feature board's slot.

const route = { hooksConfig: { route: `/p/SHIP/items/${FEATURE_KEY}/board` } };
const slot = <FeatureBoardSlot project={PROJECT} itemKey={FEATURE_KEY} />;
const THEMES = ['light', 'dark'] as const;

test('PhoneToolbar_WhenFilterOpened_ShouldListEachPersonWithTaskCount', async ({ mount, page }) => {
  await stubFeatureBoard(page, FEATURE_VIEW);
  await mount(slot, route);
  await expect(page.getByTestId(ids.lane).first()).toBeVisible();
  const filter = page.getByTestId(ids.filterButton);
  await expect(filter).toHaveText('Filter');

  await filter.tap();

  const rows = page.getByTestId(ids.filterSheetRow);
  await expect(rows).toHaveCount(3);
  await expect(rows).toContainText(['Priya Shah1', 'Tom Okafor2', 'Unassigned1']);

  await rows.nth(1).tap();
  await expect(rows.nth(1).getByRole('checkbox')).toBeChecked();
  await expect(filter).toHaveText('Filter (1)');
  const assignees = () =>
    page
      .getByTestId(ids.card)
      .evaluateAll((cards) =>
        cards.map((card) => card.querySelector('[data-variant]')?.getAttribute('title')),
      );
  await expect.poll(async () => (await assignees()).length).toBeGreaterThan(0);
  expect(new Set(await assignees())).toEqual(new Set(['Tom Okafor']));

  await page.getByTestId(ids.filterSheetClear).tap();
  await expect(rows.nth(1).getByRole('checkbox')).not.toBeChecked();
  await expect(filter).toHaveText('Filter');
});

test('PhoneToolbar_WhenMoreOpened_ShouldOfferCollapseExpandAndSettings', async ({
  mount,
  page,
}) => {
  await stubFeatureBoard(page, FEATURE_VIEW);
  await mount(slot, route);
  await expect(page.getByTestId(ids.lane).first()).toBeVisible();
  await expect(page.getByTestId(ids.settingsOpen)).toBeHidden();

  await page.getByTestId(touchTestIds.moreButton).tap();

  await expect(page.getByTestId(touchTestIds.actionSheetRow)).toHaveText([
    'Collapse all',
    'Expand all',
    'Card settings',
    'Open the Feature',
  ]);
  const lanes = page.getByTestId(ids.lane);
  await page.locator('[data-action-id="collapse-all"]').tap();
  await expect(lanes.first()).toHaveAttribute('data-collapsed', 'true');
  await page.getByTestId(touchTestIds.moreButton).tap();
  await page.locator('[data-action-id="expand-all"]').tap();
  await expect(lanes.first()).toHaveAttribute('data-collapsed', 'false');
});

for (const theme of THEMES) {
  test(`PhoneToolbar_WhenFilterOpened_ShouldMatchItsScreenshot_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubFeatureBoard(page, FEATURE_VIEW);
    await mount(slot, route);
    await expect(page.getByTestId(ids.lane).first()).toBeVisible();
    await page.getByTestId(ids.filterButton).tap();
    await page.getByTestId(ids.filterSheetRow).nth(1).tap();
    await expect(page.getByTestId(touchTestIds.sheet)).toHaveScreenshot(
      `phone-toolbar-filter.${theme}.png`,
    );
  });
}
