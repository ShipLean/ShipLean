import { expect, test } from '@playwright/experimental-ct-react';
import { story } from '../ct/fixtures';
import { stubApi } from '../ct/stubApi';
import { WorkItemFormHarness } from '../ct/WorkItemFormHarness';
import { workItemTestIds as ids } from '../testIds';
import { componentTestIds } from '../../../components/testIds';

// The tags on the CT phone project (390×844): each chip's ✕ box is touch-sized and covers nothing but its own chip, so
// a tap on the next chip or on "+ Add tag" is not taken by it.

for (const names of [
  ['Checkout', 'PCI', 'Payments'],
  ['Checkout', 'PCI', 'Payments', 'Fraud review', 'Chargebacks', 'Settlement', 'Refunds', 'Audit'],
]) {
  test(`TagsField_When${names.length}TagsOnPhone_ShouldKeepEachRemoveBoxOffItsNeighbours`, async ({
    mount,
    page,
  }) => {
    await stubApi(page, { ...story, tags: names });
    const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, {
      hooksConfig: { route: '/p/SHIP/items/SHIP-141' },
    });
    const tags = screen.getByTestId(ids.tags);
    const chips = tags.getByTestId(componentTestIds.chip);
    const removes = tags.getByTestId(componentTestIds.chipRemove);
    await expect(chips).toHaveCount(names.length);
    const n = names.length;

    const rects: { x: number; y: number; width: number; height: number }[] = [];
    for (let i = 0; i < n; i += 1) rects.push((await removes.nth(i).boundingBox())!);
    const others: { x: number; y: number; width: number; height: number }[] = [];
    for (let i = 0; i < n; i += 1) others.push((await chips.nth(i).boundingBox())!);
    others.push((await tags.getByTestId(ids.tagInput).boundingBox())!);
    const overlap = (a: (typeof rects)[0], b: (typeof rects)[0]) =>
      a.x < b.x + b.width - 0.5 &&
      b.x < a.x + a.width - 0.5 &&
      a.y < b.y + b.height - 0.5 &&
      b.y < a.y + a.height - 0.5;
    rects.forEach((remove, i) => {
      expect(remove.width, `✕ ${i}`).toBeGreaterThanOrEqual(32);
      expect(remove.height, `✕ ${i}`).toBeGreaterThanOrEqual(32);
      others.forEach((other, j) => {
        // Its own chip (index i) is the one it belongs to; every other chip and the input must be clear.
        if (j !== i) expect(overlap(remove, other), `✕ ${i} over item ${j}`).toBe(false);
      });
      rects.forEach((next, j) => {
        if (j !== i) expect(overlap(remove, next), `✕ ${i} over ✕ ${j}`).toBe(false);
      });
    });
    if (n > 3) expect(new Set(rects.map((r) => Math.round(r.y))).size, 'rows').toBeGreaterThan(1);
  });
}
