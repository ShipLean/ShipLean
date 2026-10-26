import { expect, test } from '@playwright/experimental-ct-react';
import { Chip } from './Chip';
import { componentTestIds as ids } from './testIds';

test('Chip_WhenRemoveClicked_ShouldCallBack', async ({ mount, page }) => {
  let removed = 0;
  await mount(
    <Chip tone="warn" onRemove={() => (removed += 1)}>
      Sprint 24
    </Chip>,
  );
  await expect(page.getByTestId(ids.chip)).toHaveAttribute('data-tone', 'warn');
  await page.getByTestId(ids.chipRemove).click();
  expect(removed).toBe(1);
});

test('Chip_WhenNoRemoveGiven_ShouldHaveNoRemoveButton', async ({ mount, page }) => {
  await mount(<Chip>Plain</Chip>);
  await expect(page.getByTestId(ids.chipRemove)).toHaveCount(0);
});
