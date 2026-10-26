import { expect, test } from '@playwright/experimental-ct-react';
import { story } from '../ct/fixtures';
import { stubApi } from '../ct/stubApi';
import { workItemHarnessIds as harness } from '../ct/harnessIds';
import { WorkItemFormHarness } from '../ct/WorkItemFormHarness';
import { workItemTestIds as ids } from '../testIds';

const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-141' } };
const epic = {
  id: 'e1',
  key: 'SHIP-100',
  type: { id: 'epic' as const, name: 'Epic' },
  title: 'Payments',
};
const feature = {
  ...story,
  id: 'f1',
  key: 'SHIP-110',
  type: { id: 'feature' as const, name: 'Feature' },
  title: 'Card vault',
  parent: epic,
};
const loose = { ...feature, id: 'f2', key: 'SHIP-111', title: 'Receipts', parent: null };

test('ParentField_WhenFeatureIsUnderAnEpic_ShouldShowEpicThenFeature', async ({ mount, page }) => {
  await stubApi(page, story, undefined, { byType: { feature: [feature, loose] } });
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route);

  await screen.getByTestId(ids.parentButton).click();

  const options = screen.getByTestId(ids.parentOption);
  await expect(options.nth(1)).toContainText('Payments › Card vault');
  await expect(options.nth(2)).toContainText('Receipts');
  await expect(options.nth(2)).not.toContainText('›');
  await screen.getByTestId(ids.parentSearch).fill('payments');
  await expect(options).toHaveCount(2);
});

test('ParentField_WhenOpenClicked_ShouldOpenParentModal', async ({ mount, page }) => {
  await stubApi(page, { ...story, parent: epic });
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route);

  await screen.getByTestId(ids.parentOpen).click();

  await expect(screen.getByTestId(harness.location)).toHaveText('/p/SHIP/items/SHIP-141');
  await expect(screen.getByTestId(harness.search)).toHaveText('?item=SHIP-100');
});

test('ParentField_WhenNoParent_ShouldNotOfferOpen', async ({ mount, page }) => {
  await stubApi(page, story);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route);

  await expect(screen.getByTestId(ids.parentButton)).toBeVisible();
  await expect(screen.getByTestId(ids.parentOpen)).toHaveCount(0);
});

test('ParentField_ShouldNameTheOpenLinkAfterItsKey', async ({ mount, page }) => {
  await stubApi(page, { ...story, parent: epic });
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route);

  await expect(screen.getByRole('link', { name: 'Open parent SHIP-100' })).toBeVisible();
});

test('ParentField_WhenParentTitleIsVeryLong_ShouldKeepOpenLinkInsideTheCell', async ({
  mount,
  page,
}) => {
  const title = 'A parent with a remarkably long title '.repeat(12);
  await stubApi(page, { ...story, parent: { ...epic, title } });
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route);

  const link = await screen.getByTestId(ids.parentOpen).boundingBox();
  const cell = await screen.locator('[data-field="parent"]').boundingBox();
  expect(link).not.toBeNull();
  expect(cell).not.toBeNull();
  expect(link!.x).toBeGreaterThanOrEqual(cell!.x);
  expect(link!.x + link!.width).toBeLessThanOrEqual(cell!.x + cell!.width + 1);
});
