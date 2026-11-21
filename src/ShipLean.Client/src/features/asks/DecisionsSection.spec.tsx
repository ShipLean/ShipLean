import { expect, test } from '@playwright/experimental-ct-react';
import { stubShell } from '../../pages/screenshots/shellStub';
import type { Page } from '../../pages/screenshots/shoot';
import { realtimeTestIds } from '../realtime/testIds';
import { stubHub } from '../retros/ct/retroFixtures';
import { AsksHarness } from './ct/AsksHarness';
import { waiting } from './ct/fixtures';
import { stubAsks } from './ct/stubAsks';
import type { AsksItem } from './regions';
import { asksTestIds as ids } from './testIds';

const item: AsksItem = {
  id: waiting.workItemId,
  projectId: 'p1',
  projectKey: 'SHIP',
  key: 'SHIP-141',
  type: { id: 'story', name: 'Story' },
};
const view = <AsksHarness projectKey="SHIP" item={item} />;

// The first join refetches what loaded before it, unless that first fetch was still in flight (then it is shared):
// so wait until the read count stops changing and count from there.
async function settledReads(page: Page, asks: { reads: () => number }) {
  await expect.poll(() => asks.reads()).toBeGreaterThan(0);
  let last = -1;
  while (asks.reads() !== last) {
    last = asks.reads();
    await page.waitForTimeout(300);
  }
  return last;
}

test('DecisionsSection_WhenLoaded_ShouldShowHeading', async ({ mount, page }) => {
  await stubAsks(page, waiting);
  const component = await mount(view);

  const section = component.getByTestId(ids.section);
  await expect(section.getByRole('heading', { name: 'Decisions and questions' })).toBeVisible();
  await expect(section.getByTestId(ids.loading)).toHaveCount(0);
  await expect(section.getByTestId(ids.loadFailed)).toHaveCount(0);
});

test('DecisionsSection_WhenAsksFail_ShouldShowError', async ({ mount, page }) => {
  const asks = await stubAsks(page, waiting);
  asks.failReads(404, { error: 'Not found.', code: 'not_found' });
  const component = await mount(view);

  await expect(component.getByTestId(ids.loadFailed)).toHaveText(
    'The decisions and questions could not be loaded.',
  );
  await expect(component.getByTestId(ids.loading)).toHaveCount(0);
});

test('DecisionsSection_WhenAsksChangedEventArrives_ShouldRefetch', async ({ mount, page }) => {
  await stubShell(page);
  const asks = await stubAsks(page, waiting);
  const hub = await stubHub(page);
  const component = await mount(view);
  await expect(component.getByTestId(ids.section)).toBeVisible();
  await expect(page.getByTestId(realtimeTestIds.liveDot)).toHaveAttribute('data-state', 'live');
  const before = await settledReads(page, asks);

  await hub.push({
    type: 'asksChanged',
    projectId: 'p1',
    clientId: null,
    workItemId: 'other-item',
  });
  await hub.push({ type: 'asksChanged', projectId: 'p1', clientId: null, workItemId: item.id });

  await expect.poll(() => asks.reads()).toBe(before + 1);
  await expect(component.getByTestId(ids.loadFailed)).toHaveCount(0);
});

test('DecisionsSection_WhenOtherItemAsksChange_ShouldNotRefetch', async ({ mount, page }) => {
  await stubShell(page);
  const asks = await stubAsks(page, waiting);
  const hub = await stubHub(page);
  const component = await mount(view);
  await expect(component.getByTestId(ids.section)).toBeVisible();
  await expect(page.getByTestId(realtimeTestIds.liveDot)).toHaveAttribute('data-state', 'live');
  const before = await settledReads(page, asks);

  await hub.push({
    type: 'asksChanged',
    projectId: 'p1',
    clientId: null,
    workItemId: 'other-item',
  });
  // Events are handled in order, so a refetch caused by the other item's event was already sent before this one's.
  const sentinel = page.waitForResponse((response) => response.url().endsWith(`/${item.id}/asks`));
  await hub.push({ type: 'asksChanged', projectId: 'p1', clientId: null, workItemId: item.id });
  await sentinel;

  expect(asks.reads()).toBe(before + 1);
});
