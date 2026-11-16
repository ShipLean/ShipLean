import { expect, test } from '@playwright/experimental-ct-react';
import none from '../../../../ShipLean.TestSupport/DtoFixtures/demos/sprint-demo-none.json' with { type: 'json' };
import type { SprintDemoDto } from '@shiplean/core/api/demos';
import { DemoBandHarness } from './ct/DemoBandHarness';
import { actionsWithWarning, createWarning } from './ct/fixtures';
import { DemoCreateWarning } from './DemoCreateWarning';
import { demosTestIds as ids } from './testIds';

test('DemoCreateWarning_WhenTheLastDeliveryFailed_ShouldShowTheWarningText', async ({
  mount,
  page,
}) => {
  await mount(<DemoCreateWarning actions={actionsWithWarning} />);

  await expect(page.getByTestId(ids.demoCreateWarning)).toHaveText(createWarning);
});

test('DemoCreateWarning_WhenThereIsNoWarning_ShouldRenderNothing', async ({ mount, page }) => {
  await mount(<DemoCreateWarning actions={{ ...actionsWithWarning, createWarning: null }} />);

  await expect(page.getByTestId(ids.demoCreateWarning)).toHaveCount(0);
  expect(await page.locator('#root').innerHTML()).toBe('');
});

test('DemoCreateWarning_WhenTheBandCanCreate_ShouldShowTheWarningByCreateDemo', async ({
  mount,
}) => {
  const base = none as unknown as SprintDemoDto;
  const demo = { ...base, actions: { ...base.actions, ...actionsWithWarning } };
  const view = await mount(<DemoBandHarness demo={demo} />);

  await expect(view.getByTestId(ids.demoCreate)).toBeVisible();
  await expect(view.getByTestId(ids.demoCreateWarning)).toHaveText(createWarning);
});
