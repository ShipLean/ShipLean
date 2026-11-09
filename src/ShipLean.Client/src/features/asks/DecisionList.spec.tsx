import { expect, test } from '@playwright/experimental-ct-react';
import type { Locator } from 'playwright';
import type { ItemAsksDto } from '../../api/asks';
import { DecisionList } from './DecisionList';
import type { AsksItem } from './regions';
import { asksTestIds as ids } from './testIds';
import { feature, history, waiting } from './ct/fixtures';
import { RegionHarness } from './ct/RegionHarness';
import { stubAsks } from './ct/stubAsks';

const itemFor = (asks: ItemAsksDto): AsksItem => ({
  id: asks.workItemId,
  projectId: 'p1',
  projectKey: 'SHIP',
  key: 'SHIP-141',
  type: { id: 'story', name: 'Story' },
});

const rowOf = (root: Locator, label: string) => {
  const page = root.page();
  return page.getByTestId(ids.decisionRow).filter({
    has: page.getByTestId(ids.decisionLabel).filter({ hasText: new RegExp(`^${label}$`) }),
  });
};

test('DecisionRow_WhenOpen_ShouldSayGoingWithRecommended', async ({ mount }) => {
  const component = await mount(
    <RegionHarness>
      <DecisionList item={itemFor(waiting)} asks={waiting} />
    </RegionHarness>,
  );
  const row = rowOf(component, 'D1');
  await expect(row.getByTestId(ids.decisionState)).toContainText('Going with recommended');
  await expect(row.getByTestId(ids.decisionTag)).toHaveText('Recommended');
  await expect(row.getByTestId(ids.decisionConfirm)).toBeVisible();
  await expect(rowOf(component, 'D5').getByTestId(ids.decisionState)).toContainText('✓ Confirmed');
  await expect(rowOf(component, 'D6').getByTestId(ids.decisionState)).toContainText('Changed');
  await expect(rowOf(component, 'D6').getByTestId(ids.decisionTag)).toHaveText('Alternative');
});

test('DecisionRow_WhenChangedToOwn_ShouldTagOwnAnswer', async ({ mount }) => {
  const component = await mount(
    <RegionHarness>
      <DecisionList item={itemFor(feature)} asks={feature} />
    </RegionHarness>,
  );
  const row = rowOf(component, 'D1');
  await expect(row.getByTestId(ids.decisionTag)).toHaveText('Own answer');
  await expect(row.getByTestId(ids.decisionAnswer)).toContainText(
    feature.decisions[0].currentAnswer,
  );
});

test('DecisionRow_WhenWithdrawn_ShouldStrikeThroughAndHideConfirm', async ({ mount }) => {
  const component = await mount(
    <RegionHarness>
      <DecisionList item={itemFor(waiting)} asks={waiting} />
    </RegionHarness>,
  );
  const row = rowOf(component, 'D7');
  await expect(row.getByTestId(ids.decisionState)).toContainText('Withdrawn');
  await expect(row.getByTestId(ids.decisionAnswer)).toHaveCSS(
    'text-decoration-line',
    'line-through',
  );
  await expect(row.getByTestId(ids.decisionConfirm)).toHaveCount(0);
  await expect(row.getByTestId(ids.decisionMore)).toHaveCount(0);
});

test('DecisionRow_WhenConfirmClicked_ShouldPostItsNumber', async ({ mount, page }) => {
  const stub = await stubAsks(page, waiting);
  const component = await mount(
    <RegionHarness>
      <DecisionList item={itemFor(waiting)} asks={waiting} />
    </RegionHarness>,
  );
  await rowOf(component, 'D3').getByTestId(ids.decisionConfirm).click();
  await expect.poll(() => stub.writes.length).toBe(1);
  expect(stub.writes[0]).toEqual({
    method: 'POST',
    url: `/api/work-items/${waiting.workItemId}/decisions/confirm`,
    body: { numbers: [3] },
  });
});

test('DecisionRow_WhenConfirmRefused_ShouldToastServerMessage', async ({ mount, page }) => {
  const stub = await stubAsks(page, waiting);
  stub.failNext(409, { error: 'D3 is no longer open.', code: 'conflict' });
  const component = await mount(
    <RegionHarness>
      <DecisionList item={itemFor(waiting)} asks={waiting} />
    </RegionHarness>,
  );
  await rowOf(component, 'D3').getByTestId(ids.decisionConfirm).click();
  await expect(page.getByText('D3 is no longer open.')).toBeVisible();
  await expect(rowOf(component, 'D3').getByTestId(ids.decisionConfirm)).toBeVisible();
});

test('DecisionMenu_WhenWithdrawChosen_ShouldPostWithdraw', async ({ mount, page }) => {
  const stub = await stubAsks(page, waiting);
  const component = await mount(
    <RegionHarness>
      <DecisionList item={itemFor(waiting)} asks={waiting} />
    </RegionHarness>,
  );
  const row = rowOf(component, 'D2');
  await row.getByTestId(ids.decisionMore).click();
  await row.getByTestId(ids.decisionWithdraw).click();
  await expect.poll(() => stub.writes.length).toBe(1);
  expect(stub.writes[0].url).toBe(`/api/work-items/${waiting.workItemId}/decisions/2/withdraw`);
});

test('DecisionMenu_WhenCopyLinkChosen_ShouldToast', async ({ mount, page, context }) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  const component = await mount(
    <RegionHarness>
      <DecisionList item={itemFor(waiting)} asks={waiting} />
    </RegionHarness>,
  );
  const row = rowOf(component, 'D3');
  await row.getByTestId(ids.decisionMore).click();
  await row.getByTestId(ids.decisionCopyLink).click();
  await expect(page.getByText('Link to D3 copied')).toBeVisible();
  const copied = await page.evaluate(() => navigator.clipboard.readText());
  expect(copied.endsWith('/p/SHIP/items/SHIP-141#D3')).toBe(true);
});

test('DecisionHistory_WhenOpened_ShouldListChangesWithFromAndTo', async ({ mount }) => {
  const component = await mount(
    <RegionHarness>
      <DecisionList item={itemFor(history)} asks={history} />
    </RegionHarness>,
  );
  const row = rowOf(component, 'D6');
  await expect(row.getByTestId(ids.decisionHistory)).toHaveCount(0);
  await row.getByTestId(ids.decisionHistoryToggle).click();
  const list = row.getByTestId(ids.decisionHistory);
  const changed = history.decisions[5].history[1];
  await expect(list).toContainText(`changed it to “${changed.to}”`);
  await expect(list).toContainText(changed.from ?? '');
  await expect(list).toContainText('raised it, recommending');
});

test('DecisionRow_WhenChangeClicked_ShouldOpenEditorAndCloseOnCancel', async ({ mount }) => {
  const component = await mount(
    <RegionHarness>
      <DecisionList item={itemFor(waiting)} asks={waiting} />
    </RegionHarness>,
  );
  const row = rowOf(component, 'D1');
  await row.getByTestId(ids.decisionChangeButton).click();
  await expect(row.getByTestId(ids.decisionChange)).toBeVisible();
  await row.getByTestId(ids.decisionDiscussButton).click();
  await expect(row.getByTestId(ids.decisionChange)).toHaveCount(0);
  await expect(row.getByTestId(ids.decisionDiscuss)).toBeVisible();
  await row.getByTestId(ids.discussCancel).click();
  await expect(row.getByTestId(ids.decisionDiscuss)).toHaveCount(0);
  await row.getByTestId(ids.decisionChangeButton).click();
  await row.getByTestId(ids.changeCancel).click();
  await expect(row.getByTestId(ids.decisionChange)).toHaveCount(0);
});

for (const theme of ['light', 'dark'] as const) {
  test(`DecisionList_WhenWaiting_ShouldMatchItsScreenshot_${theme}`, async ({ mount, page }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    const component = await mount(
      <RegionHarness>
        <DecisionList item={itemFor(waiting)} asks={waiting} />
      </RegionHarness>,
    );
    await expect(component.getByTestId(ids.decisionRow).first()).toBeVisible();
    await expect(component).toHaveScreenshot(`decisions-waiting.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`DecisionList_WhenHistoryOpen_ShouldMatchItsScreenshot_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    const component = await mount(
      <RegionHarness>
        <DecisionList item={itemFor(history)} asks={history} />
      </RegionHarness>,
    );
    await rowOf(component, 'D6').getByTestId(ids.decisionHistoryToggle).click();
    await expect(component.getByTestId(ids.decisionHistory)).toBeVisible();
    await expect(component).toHaveScreenshot(`decisions-history.${theme}.png`, {
      animations: 'disabled',
    });
  });
}
