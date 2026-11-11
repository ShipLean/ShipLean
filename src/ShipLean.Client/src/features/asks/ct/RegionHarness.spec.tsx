import { expect, test } from '@playwright/experimental-ct-react';
import { DecisionChange } from '../DecisionChange';
import type { AsksItem } from '../regions';
import { asksTestIds as ids } from '../testIds';
import { waiting } from './fixtures';
import { RegionHarness } from './RegionHarness';

const item: AsksItem = {
  id: waiting.workItemId,
  projectId: 'p1',
  projectKey: 'SHIP',
  key: 'SHIP-141',
  type: { id: 'story', name: 'Story' },
};
const decision = waiting.decisions[0];

test('RegionHarness_WhenDecisionChangeCancelled_ShouldCallOnClose', async ({ mount }) => {
  let closed = 0;
  const component = await mount(
    <RegionHarness>
      <DecisionChange
        item={item}
        asks={waiting}
        decision={decision}
        onClose={() => {
          closed += 1;
        }}
      />
    </RegionHarness>,
  );

  await expect(component.getByTestId(ids.decisionChange)).toBeVisible();
  await component.getByTestId(ids.changeCancel).click();
  expect(closed).toBe(1);
});
