import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../../AppRoutes';
import { stubShell } from '../../../pages/screenshots/shellStub';
import { realtimeTestIds } from '../../realtime/testIds';
import { pagesTestIds } from '../../pages/testIds';
import { shellTestIds as shell } from '../../shell/testIds';
import { sprintHeaderTestIds as header } from '../../sprintHeader/testIds';
import { retrosTestIds as ids } from '../testIds';
import { stubHub, stubRetros } from './retroFixtures';

const open = (route: string) => ({ hooksConfig: { route } });

test('RetrosMenu_WhenOpened_ShouldSitBelowSprintsWithSearchAndInsightsCount', async ({
  mount,
  page,
}) => {
  await stubShell(page);
  await stubRetros(page);
  await mount(<AppRoutes />, open('/p/SHIP/retros'));

  const menu = page.getByTestId(shell.sideMenu);
  const retros = menu.getByTestId(shell.navRetrospectives);
  await expect(retros).toContainText('Retrospectives');
  await expect(retros).toHaveAttribute('aria-current', 'page');
  const above = await retros.evaluate((el) =>
    el.previousElementSibling?.getAttribute('data-testid'),
  );
  expect(above).toBe(shell.navSprints);

  await expect(menu.getByTestId(shell.navRetrosSearch)).toHaveAttribute('aria-current', 'page');
  await expect(menu.getByTestId(shell.navRetrosInsights)).toContainText('Insights');
  await expect(menu.getByTestId(shell.navRetrosInsightsCount)).toHaveText('4 recurring');
});

test('RetrosMenu_WhenInsightsClicked_ShouldOpenTheInsightsScreen', async ({ mount, page }) => {
  await stubShell(page);
  await stubRetros(page);
  await mount(<AppRoutes />, open('/p/SHIP/retros'));

  await page.getByTestId(shell.navRetrosInsights).click();
  await expect(page.getByTestId(pagesTestIds.retroInsightsPage)).toBeVisible();
  await expect(page.getByTestId(shell.navRetrosInsights)).toHaveAttribute('aria-current', 'page');
});

test('SprintRetro_WhenOpened_ShouldShowByCategoryThenToggleToByWorkItem', async ({
  mount,
  page,
}) => {
  await stubShell(page);
  await stubRetros(page);
  await mount(<AppRoutes />, open('/p/SHIP/sprints/sprint-24/retro'));

  await expect(page.getByTestId(header.tabRetro)).toHaveAttribute('aria-current', 'page');
  await expect(page.getByTestId(ids.retroViewByCategory)).toHaveAttribute('aria-pressed', 'true');
  await expect(page.getByTestId(ids.retroViewByItem)).toHaveAttribute('aria-pressed', 'false');
  await expect(page.getByTestId(pagesTestIds.slotRetroByCategory)).toBeAttached();
  await expect(page.getByTestId(ids.retroCardList)).toHaveCount(0);
  for (const id of [ids.retroHeader, ids.retroAddPoint, ids.retroRail]) {
    await expect(page.getByTestId(id)).toBeAttached();
  }

  await page.getByTestId(ids.retroViewByItem).click();
  await expect(page.getByTestId(ids.retroViewByItem)).toHaveAttribute('aria-pressed', 'true');
  await expect(page.getByTestId(ids.retroCardList)).toBeAttached();
  await expect(page.getByTestId(pagesTestIds.slotRetroByCategory)).toHaveCount(0);
});

test('RetroInsights_WhenOpened_ShouldShowRecurringThemesAndRangeChangesTheQuery', async ({
  mount,
  page,
}) => {
  await stubShell(page);
  const stub = await stubRetros(page);
  await mount(<AppRoutes />, open('/p/SHIP/retros/insights'));

  const recurringTab = page
    .getByTestId(ids.retroInsightsTab)
    .filter({ hasText: 'Recurring themes' });
  await expect(recurringTab).toHaveAttribute('aria-pressed', 'true');
  await expect(page.getByTestId(ids.retroRecurringTable)).toBeAttached();
  await expect(page.getByTestId(ids.retroKeepsWorking)).toBeAttached();
  await expect(page.getByTestId(ids.retroMergeThemes)).toBeAttached();
  await expect(page.getByTestId(ids.retroInsightsRange)).toHaveValue('6');
  expect(stub.reads('/retros/insights').map((c) => c.query)).toContain('range=6');

  await page.getByTestId(ids.retroInsightsRange).selectOption('3');
  await expect(page.getByTestId(ids.retroInsightsRange)).toHaveValue('3');
  await expect.poll(() => stub.reads('/retros/insights').map((c) => c.query)).toContain('range=3');

  await page.getByTestId(ids.retroInsightsTab).filter({ hasText: 'Action items' }).click();
  await expect(page.getByTestId(ids.retroActionsTable)).toBeAttached();
  await expect(page.getByTestId(ids.retroRecurringTable)).toHaveCount(0);
  await page.getByTestId(ids.retroInsightsTab).filter({ hasText: 'Changes & impact' }).click();
  await expect(page.getByTestId(pagesTestIds.slotRetroChangesImpact)).toBeAttached();
});

test('RetrosSearch_WhenOpenedWithFilters_ShouldAskTheServerForThemAndKeepThemInTheAddress', async ({
  mount,
  page,
}) => {
  await stubShell(page);
  const stub = await stubRetros(page);
  await mount(<AppRoutes />, open('/p/SHIP/retros?category=improve&recurring=true&bogus=1'));

  await expect(page.getByTestId(ids.retrosSearchPanel)).toBeAttached();
  for (const id of [pagesTestIds.slotRetroSearchFilters, ids.retrosRows, ids.retrosSelected]) {
    await expect(page.getByTestId(id)).toBeAttached();
  }
  const query = new URLSearchParams(stub.reads('/retros')[0].query);
  expect(Object.fromEntries(query)).toEqual({ category: 'improve', recurring: 'true' });
});

test('RetroShell_WhenRetroChangedArrives_ShouldRefetchTheOpenScreen', async ({ mount, page }) => {
  await stubShell(page);
  const stub = await stubRetros(page);
  const hub = await stubHub(page);
  await mount(<AppRoutes />, open('/p/SHIP/sprints/sprint-24/retro'));
  await expect(page.getByTestId(ids.sprintRetroPanel)).toBeAttached();
  await expect(page.getByTestId(realtimeTestIds.liveDot)).toBeVisible();
  const before = stub.reads('/retro').length;

  await hub.push({
    type: 'retroChanged',
    projectId: 'p1',
    clientId: null,
    sprintIds: ['sprint-24'],
    workItemIds: [],
  });
  await expect.poll(() => stub.reads('/retro').length).toBeGreaterThan(before);

  // The other sprint's event first, then this sprint's: events arrive in order, so once the second one has refetched
  // the first has been handled too, and the count must be exactly one more.
  const beforeEvents = stub.reads('/retro').length;
  await hub.push({
    type: 'retroChanged',
    projectId: 'p1',
    clientId: null,
    sprintIds: ['sprint-23'],
    workItemIds: [],
  });
  await hub.push({
    type: 'retroChanged',
    projectId: 'p1',
    clientId: null,
    sprintIds: ['sprint-24'],
    workItemIds: [],
  });
  await expect.poll(() => stub.reads('/retro').length).toBe(beforeEvents + 1);
  await page.waitForTimeout(300);
  expect(stub.reads('/retro').length).toBe(beforeEvents + 1);
});

test('RetrosSearch_WhenEventIsForAnotherWorkItem_ShouldNotReadAnyWorkItemRetros', async ({
  mount,
  page,
}) => {
  await stubShell(page);
  const stub = await stubRetros(page);
  const hub = await stubHub(page);
  await mount(<AppRoutes />, open('/p/SHIP/retros'));
  await expect(page.getByTestId(ids.retrosSearchPanel)).toBeAttached();
  await expect(page.getByTestId(realtimeTestIds.liveDot)).toBeVisible();
  const searchReads = stub.reads('/retros').length;

  await hub.push({
    type: 'retroChanged',
    projectId: 'p1',
    clientId: null,
    sprintIds: [],
    workItemIds: ['w-other'],
  });
  await expect.poll(() => stub.reads('/retros').length).toBeGreaterThan(searchReads);
  await page.waitForTimeout(300);
  expect(stub.calls.filter((c) => c.path.includes('/work-items/'))).toEqual([]);
});
