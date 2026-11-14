import { expect, test } from '@playwright/experimental-ct-react';
import { story } from '../ct/fixtures';
import { stubApi } from '../ct/stubApi';
import { WorkItemFormHarness } from '../ct/WorkItemFormHarness';
import { workItemTestIds as ids } from '../testIds';

// The header on the CT phone project (390×844): a title or meta line with no break in it wraps inside the page.

const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-141' } };
const unbroken = 'https://example.com/a/very/long/unbroken/url/that/never/breaks/' + 'x'.repeat(60);

test('FormHeader_WhenTitleHasNoBreaks_ShouldNotScrollThePageSideways', async ({ mount, page }) => {
  await stubApi(page, {
    ...story,
    title: unbroken,
    createdBy: undefined,
    changedBy: undefined,
    createdAtLabel: 'just now',
    changedAtLabel: 'just now',
  } as never);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route);
  await expect(screen.getByTestId(ids.title)).toHaveValue(unbroken);

  const widths = await page.evaluate(() => ({
    scroll: document.documentElement.scrollWidth,
    client: document.documentElement.clientWidth,
  }));
  expect(widths.scroll).toBeLessThanOrEqual(widths.client);
  const header = (await screen.getByTestId(ids.header).boundingBox())!;
  const title = (await screen.getByTestId(ids.title).boundingBox())!;
  expect(title.x + title.width).toBeLessThanOrEqual(header.x + header.width);
});
