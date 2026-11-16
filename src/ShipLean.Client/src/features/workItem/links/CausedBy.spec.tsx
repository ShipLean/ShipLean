import { expect, test } from '@playwright/experimental-ct-react';
import type { CausedRefDto } from '@shiplean/core/api/workItems';
import { bug, story } from '../ct/fixtures';
import { stubApi } from '../ct/stubApi';
import { workItemTestIds as ids } from '../testIds';
import { CausedByHarness } from './CausedByHarness';

const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-153' } };
const cause = {
  ...story,
  id: 'w120',
  key: 'SHIP-120',
  title: 'Vault client',
  type: { id: 'task' as const, name: 'Task' },
};
const caused: CausedRefDto = {
  id: 'w153',
  key: 'SHIP-153',
  title: 'Crash when biometric prompt is cancelled',
};

test('CausedBy_WhenBugAddsIt_ShouldSendTheTaskAsACausedByLink', async ({ mount, page }) => {
  const api = await stubApi(page, bug, undefined, { others: [cause] });
  const screen = await mount(<CausedByHarness itemKey="SHIP-153" />, route);

  await screen.getByTestId(ids.linkAddCausedBy).click();
  await expect(screen.getByTestId(ids.linkType)).toHaveValue('causedBy');
  await screen.getByTestId(ids.linkKey).fill('ship-120');
  await screen.getByTestId(ids.linkSubmit).click();
  await expect(screen.getByTestId(ids.linkRow)).toContainText('Caused by');
  await expect(screen.getByTestId(ids.linkRow)).toContainText('Vault client');
  await screen.getByTestId(ids.save).click();

  await expect(screen.getByTestId(ids.saveState)).toHaveText('Saved · revision 8');
  expect(api.patches[0].changes.addLinks).toEqual([
    { type: 'causedBy', workItemId: 'w120', key: 'SHIP-120', title: 'Vault client', url: null },
  ]);
});

test('CausedBy_WhenAddressAsksForIt_ShouldOpenThePickerPresetToCausedBy', async ({
  mount,
  page,
}) => {
  await stubApi(page, bug, undefined, { others: [cause] });
  const screen = await mount(<CausedByHarness itemKey="SHIP-153" addLink="causedBy" />, route);

  await expect(screen.getByTestId(ids.linkForm)).toBeVisible();
  await expect(screen.getByTestId(ids.linkType)).toHaveValue('causedBy');
  await screen.getByTestId(ids.linkCancel).click();
  await expect(screen.getByTestId(ids.linkForm)).toHaveCount(0);
});

test('CausedBy_WhenTargetIsNotATask_ShouldRefuse', async ({ mount, page }) => {
  await stubApi(page, bug, undefined, {
    others: [{ ...cause, type: { id: 'story', name: 'User Story' } }],
  });
  const screen = await mount(<CausedByHarness itemKey="SHIP-153" />, route);

  await screen.getByTestId(ids.linkAddCausedBy).click();
  await screen.getByTestId(ids.linkKey).fill('SHIP-120');
  await screen.getByTestId(ids.linkSubmit).click();

  await expect(screen.getByTestId(ids.linkError)).toHaveText(
    'Caused by links to the Task that introduced the bug.',
  );
  await expect(screen.getByTestId(ids.linkRow)).toHaveCount(0);
});

test('CausedBy_WhenItemIsNotABug_ShouldNotOfferIt', async ({ mount, page }) => {
  await stubApi(page, story);
  const screen = await mount(<CausedByHarness itemKey="SHIP-141" addLink="causedBy" />, {
    hooksConfig: { route: '/p/SHIP/items/SHIP-141' },
  });

  await expect(screen.getByTestId(ids.linkAdd)).toBeVisible();
  await expect(screen.getByTestId(ids.linkAddCausedBy)).toHaveCount(0);
  await expect(screen.getByTestId(ids.linkForm)).toHaveCount(0);
  await screen.getByTestId(ids.linkAdd).click();
  await expect(screen.getByTestId(ids.linkType).locator('option[value="causedBy"]')).toHaveCount(0);
});

test('CausedBy_WhenTaskIsPointedAt_ShouldListTheBugAsCaused', async ({ mount, page }) => {
  await stubApi(page, { ...cause, caused: [caused] });
  const screen = await mount(<CausedByHarness itemKey="SHIP-120" />, {
    hooksConfig: { route: '/p/SHIP/items/SHIP-120' },
  });

  const row = screen.getByTestId(ids.linkCausedByRow);
  await expect(row).toContainText('Caused');
  await expect(row).toContainText('SHIP-153');
  await expect(row).toContainText('Crash when biometric prompt is cancelled');
  await expect(screen.getByTestId(ids.linksCount)).toHaveText('1');
  await expect(screen.getByTestId(ids.linksEmpty)).toHaveCount(0);
});
