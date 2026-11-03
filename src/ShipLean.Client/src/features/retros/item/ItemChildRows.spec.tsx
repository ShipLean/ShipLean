import { expect, test } from '@playwright/experimental-ct-react';
import type { RetroItemDto } from '../../../api/retros';
import { RETRO_CATEGORIES } from '../../../api/retros';
import { retroItem } from '../ct/retroFixtures';
import { retrosTestIds as ids } from '../testIds';
import { ItemChildRows } from './ItemChildRows';

const mountOptions = { hooksConfig: { route: '/p/SHIP/retros/items/SHIP-180' } };
const [first] = retroItem.children;
const feature: RetroItemDto = {
  ...retroItem,
  children: [
    {
      ...first,
      item: { ...first.item, key: 'SHIP-141', title: 'Pay with saved card' },
      totals: {
        counts: { wentWell: 1, notInPlan: 2, wentBadly: 3, improve: 4 },
        toDecide: 2,
        decided: 1,
        decidable: 3,
        actions: 1,
      },
    },
    {
      ...first,
      item: { ...first.item, key: 'SHIP-144', title: 'Address autocomplete' },
      totals: {
        counts: { wentWell: 0, notInPlan: 0, wentBadly: 0, improve: 0 },
        toDecide: 0,
        decided: 0,
        decidable: 0,
        actions: 0,
      },
    },
  ],
};

test('ItemChildRows_WhenAFeature_ShouldListItsStoriesAndBugsWithCountsInOrder', async ({
  mount,
  page,
}) => {
  await mount(<ItemChildRows projectKey="SHIP" item={feature} />, mountOptions);
  const rows = page.getByTestId(ids.retroItemChildRow);
  await expect(rows).toHaveCount(2);
  const cells = rows.first().locator('td[data-category]');
  expect(
    await cells.evaluateAll((tds) => tds.map((td) => td.getAttribute('data-category'))),
  ).toEqual([...RETRO_CATEGORIES]);
  expect(await cells.allTextContents()).toEqual(['1', '2', '3', '4']);
  await expect(rows.first()).toContainText('2 to decide');
  await expect(rows.first()).toContainText('1/3');
  await expect(rows.nth(1)).not.toContainText('to decide');
});

test('ItemChildRows_WhenAFeatureThenAStory_ShouldShowRowsThenNone', async ({ mount, page }) => {
  const component = await mount(<ItemChildRows projectKey="SHIP" item={feature} />, mountOptions);
  await expect(page.getByTestId(ids.retroItemChildRows)).toHaveCount(1);
  await expect(page.getByTestId(ids.retroItemChildRow)).toHaveCount(2);
  await component.update(<ItemChildRows projectKey="SHIP" item={{ ...retroItem, children: [] }} />);
  await expect(page.getByTestId(ids.retroItemChildRows)).toHaveCount(0);
  await expect(page.getByTestId(ids.retroItemChildRow)).toHaveCount(0);
});

test('ItemChildRows_WhenARowIsOpened_ShouldLinkToTheItemsOwnRetroPage', async ({ mount, page }) => {
  await mount(<ItemChildRows projectKey="SHIP" item={feature} />, mountOptions);
  await expect(page.getByRole('link', { name: 'Pay with saved card' })).toHaveAttribute(
    'href',
    '/p/SHIP/retros/items/SHIP-141',
  );
});
