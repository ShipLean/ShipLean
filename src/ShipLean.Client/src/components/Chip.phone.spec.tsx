import { expect, test } from '@playwright/experimental-ct-react';
import { Chip } from './Chip';
import { componentTestIds as ids } from './testIds';

// The remove ✕ on a phone (SHIP-805 rule 13): a 32px hit area that leaves the chip's size as it is on desktop.

test('Chip_WhenRemovableOnPhone_ShouldHaveTouchSizedRemoveAndSameChipSize', async ({
  mount,
  page,
}) => {
  let removed = 0;
  await mount(
    <Chip tone="warn" onRemove={() => (removed += 1)}>
      Sprint 24
    </Chip>,
  );

  const remove = (await page.getByTestId(ids.chipRemove).boundingBox())!;
  const chip = (await page.getByTestId(ids.chip).boundingBox())!;
  expect(remove.width).toBeGreaterThanOrEqual(32);
  expect(remove.height).toBeGreaterThanOrEqual(32);
  // The chip keeps its 20px height and its desktop width, 74px with this label.
  expect(chip.height).toBe(20);
  expect(chip.width).toBeCloseTo(74, 0);
  // The ✕ stays where it was drawn: inside the chip, near its right edge.
  expect(remove.x + remove.width / 2).toBeGreaterThan(chip.x + chip.width - 12);
  expect(remove.x + remove.width / 2).toBeLessThan(chip.x + chip.width);

  await page.getByTestId(ids.chipRemove).tap();
  expect(removed).toBe(1);
});
