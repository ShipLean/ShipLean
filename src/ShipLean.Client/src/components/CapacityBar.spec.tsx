import { expect, test } from '@playwright/experimental-ct-react';
import { CapacityBar } from './CapacityBar';
import { componentTestIds as ids } from './testIds';

test('CapacityBar_WhenOverCapacity_ShouldShowTheOverTone', async ({ mount, page }) => {
  await mount(<CapacityBar used={30} capacity={20} />);
  await expect(page.getByTestId(ids.capacityBar)).toHaveAttribute('data-tone', 'over');
});

test('CapacityBar_WhenWithinCapacity_ShouldShowTheNormalTone', async ({ mount, page }) => {
  await mount(<CapacityBar used={10} capacity={20} />);
  await expect(page.getByTestId(ids.capacityBar)).toHaveAttribute('data-tone', 'normal');
  await expect(page.getByTestId(ids.capacityBar)).toHaveAttribute('aria-valuenow', '10');
});
