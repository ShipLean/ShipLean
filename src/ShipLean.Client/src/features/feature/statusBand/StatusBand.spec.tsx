import { expect, test } from '@playwright/experimental-ct-react';
import type { StateDto } from '../../../api/projects';
import type { WorkItemPatch } from '../../../api/workItems';
import { priya } from '../../workItem/ct/fixtures';
import { stubApi } from '../../workItem/ct/stubApi';
import { stubComments } from '../../workItem/ct/stubDetails';
import { stubSide } from '../../workItem/ct/stubSide';
import { WorkItemFormHarness } from '../../workItem/ct/WorkItemFormHarness';
import { workItemTestIds } from '../../workItem/testIds';
import { feature, stubFeature } from '../ct/stubFeature';
import { featureTestIds as ids } from '../testIds';

const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-100' } };

type StubbedPage = Parameters<typeof stubApi>[0] &
  Parameters<typeof stubSide>[0] &
  Parameters<typeof stubComments>[0] &
  Parameters<typeof stubFeature>[0];

const featureStates: StateDto[] = [
  { id: 's-idea', type: 'feature', name: 'Idea', category: 'proposed', order: 1, itemCount: 0 },
  {
    id: 's-active',
    type: 'feature',
    name: 'Active',
    category: 'inProgress',
    order: 2,
    itemCount: 1,
  },
  {
    id: 's-blocked',
    type: 'feature',
    name: 'Blocked',
    category: 'inProgress',
    order: 3,
    itemCount: 0,
  },
];

async function stubAll(page: StubbedPage, item = feature) {
  const api = await stubApi(page, item);
  await stubSide(page, priya, [], []);
  await stubComments(page, priya, []);
  await stubFeature(page);
  await page.route('**/api/projects/p1/states', (r) =>
    r.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(featureStates),
    }),
  );
  return api;
}

test('StatusBand_WhenStale_ShouldShowWarn', async ({ mount, page }) => {
  const at = feature.statusVerified;
  if (at === null) throw new Error('the Feature fixture carries a status stamp');
  await stubAll(page, {
    ...feature,
    statusVerified: { ...at, atLabel: '20 days ago', stale: true },
  });
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);

  const chip = screen.getByTestId(ids.statusVerified);
  await expect(chip).toHaveText('⚠ Verified 20 days ago');
  await expect(chip).toHaveAttribute('data-stale', 'true');
  await expect(screen.getByTestId(ids.statusVerifiedBy)).toContainText('set_status');
});

test('StatusBand_WhenDirty_ShouldDisableStillTrue', async ({ mount, page }) => {
  await stubAll(page);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);
  const stillTrue = screen.getByTestId(ids.statusStillTrue);
  await expect(stillTrue).toBeEnabled();

  await screen.getByTestId(workItemTestIds.title).fill('Checkout v3');

  await expect(stillTrue).toBeDisabled();
  await expect(stillTrue).toHaveAttribute('title', 'Save or discard your changes first');
});

test('StatusBand_WhenEditStatusSaved_ShouldSendStateAndStatus', async ({ mount, page }) => {
  const api = await stubAll(page);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);

  await screen.getByTestId(ids.statusEdit).click();
  await screen.getByTestId(ids.statusEditState).click();
  await screen.getByTestId('state-menu-option').filter({ hasText: 'Blocked' }).click();
  await screen.getByTestId(ids.statusEditLine).fill('Waiting on the vault rate limit.');
  await screen.getByTestId(ids.statusEditSave).click();
  await expect(screen.getByTestId(ids.statusLine)).toHaveText('Waiting on the vault rate limit.');
  await screen.getByTestId(workItemTestIds.save).click();

  await expect(screen.getByTestId(workItemTestIds.saveState)).toHaveText(
    `Saved · revision ${feature.revision + 1}`,
  );
  expect(api.patches).toHaveLength(1);
  const changes = (api.patches[0] as WorkItemPatch).changes;
  expect(changes.state).toEqual({ value: 's-blocked' });
  expect(changes.statusLine).toEqual({ value: 'Waiting on the vault rate limit.' });
});
