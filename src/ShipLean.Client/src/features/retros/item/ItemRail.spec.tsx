import { expect, test } from '@playwright/experimental-ct-react';
import { retroItem } from '../ct/retroFixtures';
import { retrosTestIds as ids } from '../testIds';
import { ItemRail } from './ItemRail';

const mountOptions = { hooksConfig: { route: '/p/SHIP/retros/items/SHIP-180' } };

test('ItemRail_WhenOpened_ShouldListActionItemsAndCompiledFrom', async ({ mount, page }) => {
  await mount(<ItemRail projectKey="SHIP" item={retroItem} />, mountOptions);
  const actions = page.getByTestId(ids.retroItemRailActions);
  for (const action of retroItem.actions) {
    const row = actions.locator(`div[data-item-key="${action.key}"]`);
    await expect(row).toContainText(action.title);
    await expect(row).toContainText(action.state.name);
    await expect(row.locator('[data-planned]')).toContainText(action.sprint?.name ?? 'No sprint');
  }
  const compiled = page.getByTestId(ids.retroItemRailCompiled);
  for (const ref of retroItem.cards[0].compiledFrom) {
    await expect(compiled.getByRole('link', { name: `${ref.key} ${ref.title}` })).toHaveAttribute(
      'href',
      `/p/SHIP/retros/items/${ref.key}`,
    );
  }
  const writers = page.getByTestId(ids.retroItemRailWriters);
  await expect(writers).toContainText(retroItem.cards[0].author.agentName!);
  await expect(writers).toContainText('Conductor');
  await expect(writers).toContainText(retroItem.cards[0].createdAtLabel);
});

test('ItemRail_WhenThereAreNoActionsOrCompiledFrom_ShouldSayNoneAndOmitCompiledFrom', async ({
  mount,
  page,
}) => {
  const bare = {
    ...retroItem,
    actions: [],
    cards: retroItem.cards.map((card) => ({ ...card, compiledFrom: [] })),
  };
  await mount(<ItemRail projectKey="SHIP" item={bare} />, mountOptions);
  await expect(page.getByTestId(ids.retroItemRailActions)).toContainText('None yet.');
  await expect(page.getByTestId(ids.retroItemRailCompiled)).toHaveCount(0);
});
