import { expect, test } from '@playwright/experimental-ct-react';
import ready from '../../../../ShipLean.TestSupport/DtoFixtures/demos/sprint-demo-ready.json' with { type: 'json' };
import type { SprintDemoDto } from '../../api/demos';
import { DemoPlayerHarness } from './ct/DemoPlayerHarness';
import { demosTestIds as ids } from './testIds';

const demo = ready as unknown as SprintDemoDto;

test('Script_ShouldRenderHtmlAndShowEmptyState', async ({ mount }) => {
  const component = await mount(<DemoPlayerHarness demo={demo} />);
  const script = component.getByTestId(ids.demoScript);
  await expect(script.getByRole('heading', { name: 'Checkout v2' })).toBeVisible();
  await expect(script.locator('strong').first()).toHaveText('Ann:');
  await component.unmount();

  const empty = await mount(<DemoPlayerHarness demo={{ ...demo, scriptHtml: null }} />);
  await expect(empty.getByTestId(ids.demoScript)).toHaveText('This demo has no script.');
  await expect(empty.getByTestId(ids.demoScript).getByRole('heading')).toHaveCount(0);
});
