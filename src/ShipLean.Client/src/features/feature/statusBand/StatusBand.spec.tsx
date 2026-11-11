import { expect, test } from '@playwright/experimental-ct-react';
import type { StateDto } from '@shiplean/core/api/projects';
import type { WorkItemPatch } from '@shiplean/core/api/workItems';
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
  {
    id: 's-idea',
    type: 'feature',
    name: 'Idea',
    category: 'proposed',
    order: 1,
    itemCount: 0,
  },
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

async function stubAll(
  page: StubbedPage,
  item = feature,
  respondToPatch?: Parameters<typeof stubApi>[2],
) {
  const api = await stubApi(page, item, respondToPatch);
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

const priyaRef = feature.healthSet?.by ?? null;
const atRisk = (agent: string | null = null) => ({
  ...feature,
  health: 'atRisk' as const,
  healthSet: {
    at: '2026-10-19T09:12:00+00:00',
    atLabel: '19 Oct',
    by: agent === null ? priyaRef : null,
    agent,
  },
});

for (const scheme of ['light', 'dark'] as const) {
  test(`StatusBand_WhenHealthAtRisk_ShouldShowPillAndWhoSetIt_${scheme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: scheme });
    await stubAll(page, atRisk());
    const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);

    await expect(screen.getByTestId(ids.statusHealth)).toHaveText('At risk');
    await expect(screen.getByTestId(ids.statusHealthSetBy)).toHaveText('Set by Priya Shah 19 Oct');
    await expect(screen.getByTestId(ids.statusHealthSelect)).toHaveValue('atRisk');
    await expect(screen.getByTestId(ids.statusBand)).toHaveScreenshot(
      `status-band-health-at-risk-${scheme}.png`,
    );
  });
}

test('StatusBand_WhenNoHealthOrOnTrack_ShouldShowNoPill', async ({ mount, page }) => {
  await stubAll(page, { ...feature, health: 'onTrack' });
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);
  await expect(screen.getByTestId(ids.statusHealthSelect)).toHaveValue('onTrack');
  await expect(screen.getByTestId(ids.statusHealthSelect).locator('option:checked')).toHaveText(
    'On track',
  );
  await expect(screen.getByTestId(ids.statusHealth)).toHaveCount(0);
  await expect(screen.getByTestId(ids.statusHealthSetBy)).toHaveCount(0);
});

test('StatusBand_WhenNoHealth_ShouldShowNoPill', async ({ mount, page }) => {
  await stubAll(page, { ...feature, health: null, healthSet: null });
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);
  await expect(screen.getByTestId(ids.statusHealthSelect)).toHaveValue('');
  await expect(screen.getByTestId(ids.statusHealth)).toHaveCount(0);
});

test('StatusBand_WhenHealthChosenAndSaved_ShouldSendHealthInPatch', async ({ mount, page }) => {
  const api = await stubAll(page);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);

  await screen.getByTestId(ids.statusHealthSelect).selectOption('blocked');
  await expect(screen.getByTestId(ids.statusHealth)).toHaveText('Blocked');
  expect(api.patches).toHaveLength(0);
  await expect(screen.getByTestId(workItemTestIds.saveState)).toHaveText('Unsaved: Health');
  await screen.getByTestId(workItemTestIds.save).click();

  await expect(screen.getByTestId(workItemTestIds.saveState)).toHaveText(
    `Saved · revision ${feature.revision + 1}`,
  );
  expect(api.patches).toHaveLength(1);
  const changes = (api.patches[0] as WorkItemPatch).changes;
  expect(changes.health).toEqual({ value: 'blocked' });
  expect(changes.statusLine).toBeNull();
});

test('StatusBand_WhenHealthCleared_ShouldSendNullValue', async ({ mount, page }) => {
  const api = await stubAll(page, atRisk());
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);

  await screen.getByTestId(ids.statusHealthSelect).selectOption({ label: 'No health' });
  await screen.getByTestId(workItemTestIds.save).click();

  await expect(screen.getByTestId(workItemTestIds.saveState)).toHaveText(
    `Saved · revision ${feature.revision + 1}`,
  );
  expect((api.patches[0] as WorkItemPatch).changes.health).toEqual({ value: null });
});

test('StatusBand_WhenServerRejectsHealth_ShouldShowErrorInBand', async ({ mount, page }) => {
  await stubAll(page, feature, () => ({
    status: 400,
    body: {
      error: 'Health must be On track, At risk or Blocked.',
      code: 'validation',
      field: 'health',
    },
  }));
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);

  await screen.getByTestId(ids.statusHealthSelect).selectOption('blocked');
  await screen.getByTestId(workItemTestIds.save).click();

  await expect(screen.getByTestId(ids.statusError)).toHaveText(
    'Health must be On track, At risk or Blocked.',
  );
});

test('StatusBand_WhenSetByAgent_ShouldNameTheAgent', async ({ mount, page }) => {
  await stubAll(page, atRisk('Claude Code (claude.ai)'));
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);
  await expect(screen.getByTestId(ids.statusHealthSetBy)).toHaveText(
    'Set by Claude Code (claude.ai) (agent) 19 Oct',
  );
});
