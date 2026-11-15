import { expect, test } from '@playwright/experimental-ct-react';
import { story } from '../ct/fixtures';
import { stubApi } from '../ct/stubApi';
import { WorkItemFormHarness } from '../ct/WorkItemFormHarness';
import { workItemTestIds as ids } from '../testIds';

// The description's View and Edit controls, and the editor's toolbar once Edit is open, on the CT phone project
// (390×844, SHIP-805 rule 13).

const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-141' } };

test('MarkdownSection_WhenOnPhone_ShouldHaveTouchSizedViewAndEdit', async ({ mount, page }) => {
  await stubApi(page, story);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route);

  for (const id of [ids.descriptionView, ids.descriptionEdit]) {
    const box = (await screen.getByTestId(id).boundingBox())!;
    expect(box.height, id).toBeGreaterThanOrEqual(32);
    expect(box.width, id).toBeGreaterThanOrEqual(32);
  }
});

test('MarkdownSection_WhenEditOpenedOnPhone_ShouldHaveTouchSizedToolbar', async ({
  mount,
  page,
}) => {
  await stubApi(page, story);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route);

  await screen.getByTestId(ids.descriptionEdit).tap();

  const bold = screen.getByTestId(ids.description).getByRole('button', { name: /Add bold text/ });
  await expect(bold).toBeVisible();
  const box = (await bold.boundingBox())!;
  expect(box.width).toBeGreaterThanOrEqual(32);
  expect(box.height).toBeGreaterThanOrEqual(32);
  const widths = await page.evaluate(() => ({
    scroll: document.documentElement.scrollWidth,
    client: document.documentElement.clientWidth,
  }));
  expect(widths.scroll).toBeLessThanOrEqual(widths.client);
});
