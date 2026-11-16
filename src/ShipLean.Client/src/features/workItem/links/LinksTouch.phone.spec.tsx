import { expect, test } from '@playwright/experimental-ct-react';
import type { LinkDto } from '@shiplean/core/api/workItems';
import { itemPeekTestIds } from '../../itemPeek/testIds';
import { story } from '../ct/fixtures';
import { stubApi } from '../ct/stubApi';
import { WorkItemFormHarness } from '../ct/WorkItemFormHarness';
import { workItemTestIds as ids } from '../testIds';

// The Links rows on the CT phone project (390×844, SHIP-714 L714d, SHIP-1626 SHELL-2).

const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-141' } };
const related: LinkDto = {
  id: 'l1',
  type: 'related',
  workItemId: 'w144',
  key: 'SHIP-144',
  title: 'Saved cards list on the account page that goes on for a good while',
  url: null,
  card: null,
  autoLinked: null,
};
const web: LinkDto = {
  id: 'l2',
  type: 'url',
  workItemId: null,
  key: null,
  title: 'Card vault notes',
  url: 'https://wiki.example.com/card-vault',
  card: null,
  autoLinked: null,
};

test('ItemLinks_AtPhoneWidth_ShouldKeepEachKeyOnOneLine', async ({ mount, page }) => {
  await stubApi(page, { ...story, links: [related, web] });
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" realLinks />, route);
  const rows = screen.getByTestId(ids.linkRow);
  await expect(rows).toHaveCount(2);

  const key = rows.first().getByTestId(itemPeekTestIds.link);
  await expect(key).toHaveText('SHIP-144');
  const box = (await key.boundingBox())!;
  // One text line in a touch-target-high box; a key broken after its hyphen is two lines, about 42px.
  expect(box.height).toBeLessThan(36);
  // The row's width is for the key and title, so the title starts next to the key, not in a column after the type.
  const row = (await rows.first().boundingBox())!;
  expect(box.x - row.x).toBeLessThan(8);
});
