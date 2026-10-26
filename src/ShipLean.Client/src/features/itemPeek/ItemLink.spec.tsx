import { expect, test } from '@playwright/experimental-ct-react';
import { componentTestIds } from '../../components/testIds';
import { stubProjectApi } from '../../pages/pagesFixtures';
import { pagesTestIds } from '../pages/testIds';
import { bug, members, sprints, states, story } from '../workItem/ct/fixtures';
import { workItemTestIds } from '../workItem/testIds';
import { ItemPeekHarness } from './ct/ItemPeekHarness';
import { itemPeekTestIds as ids } from './testIds';

// The shell stubs, then the work item reads for the project the shell stubs (id-SHIP).
interface StubRoute {
  fulfill(response: { status: number; contentType: string; body: string }): Promise<void>;
  request(): { url(): string };
}
async function stub(page: {
  route(url: string, handler: (route: StubRoute) => Promise<void>): Promise<unknown>;
}) {
  await stubProjectApi(page);
  const json = (body: unknown, status = 200) => ({
    status,
    contentType: 'application/json',
    body: JSON.stringify(body),
  });
  const items = [story, bug].map((i) => ({ ...i, projectId: 'id-SHIP' }));
  await page.route('**/api/projects/id-SHIP/work-items/by-key/*', (route) => {
    const key = decodeURIComponent(route.request().url().split('/').pop() ?? '');
    const found = items.find((i) => i.key === key);
    return route.fulfill(
      found ? json(found) : json({ error: 'Work item not found.', code: 'not_found' }, 404),
    );
  });
  await page.route('**/api/projects/id-SHIP/states', (route) => route.fulfill(json(states)));
  await page.route('**/api/projects/id-SHIP/members', (route) => route.fulfill(json(members)));
  await page.route('**/api/projects/id-SHIP/sprints', (route) => route.fulfill(json(sprints)));
  await page.route('**/api/projects/id-SHIP/work-items?*', (route) => route.fulfill(json([])));
  await page.route('**/api/markdown/render', (route) =>
    route.fulfill(json({ markdown: '', html: '', images: [] })),
  );
}

const at = (route: string) => ({ hooksConfig: { route } });

test('ItemLink_WhenOnBoard_ShouldOpenModalAndKeepPage', async ({ mount, page }) => {
  await stub(page);
  await mount(<ItemPeekHarness itemKey="SHIP-141" />, at('/p/SHIP/sprints'));
  await expect(page.getByTestId(pagesTestIds.sprintsPage)).toBeVisible();

  await page.getByTestId(ids.link).click();

  const dialog = page.getByTestId(ids.dialog);
  await expect(dialog.getByTestId(workItemTestIds.title)).toHaveValue('Pay with saved card');
  await expect(dialog.getByTestId(workItemTestIds.back)).toHaveCount(0);
  await expect(page.getByTestId(pagesTestIds.sprintsPage)).toBeAttached();
  await expect(page.getByTestId('harness-address')).toHaveText('/p/SHIP/sprints?item=SHIP-141');
});

test('ItemLink_WhenOnBacklog_ShouldNavigateToFullPage', async ({ mount, page }) => {
  await stub(page);
  await mount(<ItemPeekHarness itemKey="SHIP-141" />, at('/p/SHIP/backlogs/stories'));

  await page.getByTestId(ids.link).click();

  await expect(page.getByTestId(pagesTestIds.workItemPage)).toBeVisible();
  await expect(page.getByTestId(ids.dialog)).toHaveCount(0);
  await expect(page.getByTestId('harness-address')).toHaveText('/p/SHIP/items/SHIP-141');
});

test('ItemLink_WhenModifierClick_ShouldNotOpenModal', async ({ mount, page }) => {
  await stub(page);
  await mount(<ItemPeekHarness itemKey="SHIP-141" />, at('/p/SHIP/sprints'));
  const link = page.getByTestId(ids.link);
  await expect(link).toHaveAttribute('href', '/p/SHIP/items/SHIP-141');

  // The browser opens the full page in a new tab; nothing happens here.
  await link.click({ modifiers: ['Control'] });

  await expect(page.getByTestId(ids.dialog)).toHaveCount(0);
  await expect(page.getByTestId('harness-address')).toHaveText('/p/SHIP/sprints');
});

test('ItemPeek_WhenEscPressed_ShouldCloseAndDropItemFromUrl', async ({ mount, page }) => {
  await stub(page);
  await mount(<ItemPeekHarness itemKey="SHIP-141" />, at('/p/SHIP/sprints?item=SHIP-141'));
  await expect(page.getByTestId(ids.dialog)).toBeVisible();

  await page.keyboard.press('Escape');

  await expect(page.getByTestId(ids.dialog)).toHaveCount(0);
  await expect(page.getByTestId('harness-address')).toHaveText('/p/SHIP/sprints');
});

test('ItemPeek_WhenOpenedFromALink_ShouldCloseBackToThePageAndSwapOnSecondOpen', async ({
  mount,
  page,
}) => {
  await stub(page);
  await mount(<ItemPeekHarness itemKey="SHIP-141" />, at('/p/SHIP/sprints'));
  await page.getByTestId(ids.link).click();
  await expect(page.getByTestId(ids.dialog)).toBeVisible();

  await page.getByTestId(ids.close).click();

  await expect(page.getByTestId(ids.dialog)).toHaveCount(0);
  await expect(page.getByTestId('harness-address')).toHaveText('/p/SHIP/sprints');
});

test('ItemPeek_WhenItemUnknown_ShouldShowLoadFailedInsideTheModal', async ({ mount, page }) => {
  await stub(page);
  await mount(<ItemPeekHarness itemKey="SHIP-1" />, at('/p/SHIP/sprints?item=SHIP-999'));

  await expect(page.getByTestId(ids.dialog).getByTestId(workItemTestIds.loadFailed)).toBeVisible();
});

test('ItemPeek_WhenModalFormDirty_ShouldRefuseClose', async ({ mount, page }) => {
  await stub(page);
  await mount(<ItemPeekHarness itemKey="SHIP-141" />, at('/p/SHIP/sprints?item=SHIP-141'));
  const dialog = page.getByTestId(ids.dialog);
  await dialog.getByTestId(workItemTestIds.points).fill('8');

  await page.keyboard.press('Escape');

  await expect(page.getByTestId(componentTestIds.toast)).toContainText('unsaved changes');
  await expect(dialog).toBeVisible();
  await expect(page.getByTestId('harness-address')).toHaveText('/p/SHIP/sprints?item=SHIP-141');
});

test('ItemPeek_WhenPageFormDirty_ShouldStillOpenModal', async ({ mount, page }) => {
  await stub(page);
  await mount(<ItemPeekHarness itemKey="SHIP-153" />, at('/p/SHIP/items/SHIP-141'));
  await page.getByTestId(workItemTestIds.points).fill('8');

  await page.getByTestId(ids.link).click();

  const dialog = page.getByTestId(ids.dialog);
  await expect(dialog.getByTestId(workItemTestIds.title)).toHaveValue(
    'Crash when biometric prompt is cancelled',
  );
  await expect(page.getByTestId('harness-address')).toHaveText(
    '/p/SHIP/items/SHIP-141?item=SHIP-153',
  );
});
