import { expect, test } from '@playwright/experimental-ct-react';
import ready from '../../../../ShipLean.TestSupport/DtoFixtures/demos/sprint-demo-ready.json' with { type: 'json' };
import type { SprintDemoDto } from '../../api/demos';
import { componentTestIds } from '../../components/testIds';
import { DemoPlayerHarness } from './ct/DemoPlayerHarness';
import { demosTestIds as ids } from './testIds';

const demo = ready as unknown as SprintDemoDto;

test('Features_ShouldLinkEachWithItsState', async ({ mount }) => {
  const component = await mount(<DemoPlayerHarness demo={demo} />);

  const rows = component.getByTestId(ids.demoFeature);
  await expect(rows).toHaveCount(demo.features.length);
  for (const [index, feature] of demo.features.entries()) {
    await expect(rows.nth(index).getByRole('link')).toHaveText(feature.key);
    await expect(rows.nth(index).getByRole('link')).toHaveAttribute('href', feature.url);
    await expect(rows.nth(index).getByTestId(componentTestIds.stateDot)).toHaveText(
      feature.state.name,
    );
    await expect(rows.nth(index)).toContainText(feature.title);
  }
});
