import { expect, test } from '@playwright/experimental-ct-react';
import type { AskHistoryLineDto } from '@shiplean/core/api/asks';
import { mei, story } from '../workItem/ct/fixtures';
import { SideRailHarness } from '../workItem/ct/SideRailHarness';
import { stubApi } from '../workItem/ct/stubApi';
import { change, revision, stubSide } from '../workItem/ct/stubSide';
import { workItemTestIds as itemIds } from '../workItem/testIds';
import { RegionHarness } from './ct/RegionHarness';
import { askHistory } from './ct/fixtures';
import { AskHistoryLine } from './AskHistoryLine';
import { asksTestIds as ids } from './testIds';

const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-141' } };
const line = (badge: string): AskHistoryLineDto => {
  const found = askHistory.asks.find((ask) => ask.badge === badge);
  if (!found) throw new Error(`No fixture line ${badge}`);
  return found;
};

test('HistorySection_WhenAsksAndRevisions_ShouldInterleaveNewestFirst', async ({ mount, page }) => {
  await stubApi(page, story);
  await stubSide(
    page,
    mei,
    [],
    [
      revision(2, mei, [change('points', 3, 5)], { changedAt: '2026-11-04T09:00:00+00:00' }),
      revision(1, mei, [change('title', null, 'Pay with saved card', null)], {
        changedAt: '2026-11-03T09:14:00+00:00',
      }),
    ],
    askHistory.asks,
  );
  const screen = await mount(<SideRailHarness itemKey="SHIP-141" />, route);

  const items = screen
    .getByTestId(itemIds.history)
    .getByTestId(itemIds.revision)
    .or(screen.getByTestId(itemIds.history).getByTestId(ids.historyLine));
  await expect(items).toHaveCount(10);
  const order = await items.evaluateAll((els) =>
    els.map((el) => el.getAttribute('data-badge') ?? `rev${el.getAttribute('data-rev')}`),
  );
  expect(order).toEqual(['D7', 'rev2', 'Q2', 'D6', 'D5', 'D2', 'Q2', 'Q1', 'rev1', 'D1–D7']);
  await expect(screen.getByTestId(itemIds.historyCount)).toHaveText('2 revisions');
  await expect(screen.getByTestId(itemIds.historyHint)).toContainText(
    "Decisions and questions aren't fields, so they don't add a revision.",
  );
});

test('AskHistoryLine_WhenFoldedRaise_ShouldShowRangeBadge', async ({ mount }) => {
  const component = await mount(
    <RegionHarness>
      <AskHistoryLine line={line('D1–D7')} />
    </RegionHarness>,
  );

  const row = component.getByTestId(ids.historyLine);
  await expect(row).toContainText('D1–D7');
  await expect(row).toContainText('raised D1–D7');
  await expect(row).toContainText('Claude Code (claude.ai)');
  await expect(row.getByTitle('Agent: Claude Code (claude.ai)')).toBeVisible();
  await expect(row).toContainText('3 Nov');
});

test('AskHistoryLine_WhenChanged_ShouldShowFromAndTo', async ({ mount }) => {
  const component = await mount(
    <RegionHarness>
      <AskHistoryLine line={line('D6')} />
    </RegionHarness>,
  );

  const row = component.getByTestId(ids.historyLine);
  await expect(row).toContainText('Joel Dickson');
  await expect(row).toContainText(
    'changed D6: No, brand and last four digits only → Only when two cards share a brand',
  );
  await expect(row.locator('b', { hasText: 'Only when two cards share a brand' })).toBeVisible();
  await expect(row.locator('b', { hasText: 'No, brand' })).toHaveCount(0);
});

test('AskHistoryLine_WhenAnswerIsLong_ShouldCutItToFiftyEightCharactersAndAnEllipsis', async ({
  mount,
}) => {
  const long = 'x'.repeat(61);
  const component = await mount(
    <RegionHarness>
      <AskHistoryLine line={{ ...line('D5'), to: long }} />
      <AskHistoryLine line={{ ...line('D5'), to: 'y'.repeat(60) }} />
    </RegionHarness>,
  );

  const rows = component.getByTestId(ids.historyLine);
  await expect(rows.nth(0)).toContainText(`confirmed D5: ${'x'.repeat(58)}…`);
  await expect(rows.nth(0)).not.toContainText('x'.repeat(59));
  await expect(rows.nth(1)).toContainText(`confirmed D5: ${'y'.repeat(60)}`);
});

test('AskHistoryLine_WhenChangedFromIsLong_ShouldCutItLikeTo', async ({ mount }) => {
  const component = await mount(
    <RegionHarness>
      <AskHistoryLine line={{ ...line('D6'), from: 'f'.repeat(61), to: 'Short' }} />
    </RegionHarness>,
  );

  const row = component.getByTestId(ids.historyLine);
  await expect(row).toContainText(`${'f'.repeat(58)}… → Short`);
  await expect(row).not.toContainText('f'.repeat(59));
});

test('AskHistoryLine_WhenEachKind_ShouldUseTheDesignWording', async ({ mount }) => {
  const component = await mount(
    <RegionHarness>
      <AskHistoryLine line={line('Q1')} />
      <AskHistoryLine line={line('D5')} />
      <AskHistoryLine line={line('D2')} />
      <AskHistoryLine line={line('D7')} />
      <AskHistoryLine line={{ ...line('Q2'), kind: 'answered', to: 'Wait for !57' }} />
    </RegionHarness>,
  );

  const rows = component.getByTestId(ids.historyLine);
  await expect(rows.nth(0)).toContainText('asked Q1');
  await expect(rows.nth(1)).toContainText('confirmed D5: Three');
  await expect(rows.nth(2)).toContainText('edited D2');
  await expect(rows.nth(2)).not.toContainText('Greyed out');
  await expect(rows.nth(3)).toContainText('withdrew D7');
  await expect(rows.nth(4)).toContainText('answered Q2: Wait for !57');
});

test('HistorySection_WhenARevisionAndAnAskShareATime_ShouldListTheRevisionFirst', async ({
  mount,
  page,
}) => {
  const at = '2026-11-04T09:00:00+00:00';
  await stubApi(page, story);
  await stubSide(
    page,
    mei,
    [],
    [revision(1, mei, [change('points', 3, 5)], { changedAt: at })],
    [{ ...line('D5'), at }],
  );
  const screen = await mount(<SideRailHarness itemKey="SHIP-141" />, route);

  const history = screen.getByTestId(itemIds.history);
  const items = history.getByTestId(itemIds.revision).or(history.getByTestId(ids.historyLine));
  await expect(items).toHaveCount(2);
  await expect(items.nth(0)).toHaveAttribute('data-rev', '1');
  await expect(items.nth(1)).toHaveAttribute('data-badge', 'D5');
});

for (const theme of ['light', 'dark'] as const) {
  test(`HistorySection_WhenRendered_ShouldMatchItsScreenshot_${theme}`, async ({ mount, page }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubApi(page, story);
    await stubSide(
      page,
      mei,
      [],
      [revision(1, mei, [change('title', null, 'Pay with saved card', null)])],
      askHistory.asks,
    );
    const screen = await mount(<SideRailHarness itemKey="SHIP-141" />, route);
    const history = screen.getByTestId(itemIds.history);
    await expect(
      history.getByTestId(itemIds.revision).or(history.getByTestId(ids.historyLine)),
    ).toHaveCount(9);
    await expect(history).toHaveScreenshot(`asks-history-rail.${theme}.png`, {
      animations: 'disabled',
    });
  });
}
