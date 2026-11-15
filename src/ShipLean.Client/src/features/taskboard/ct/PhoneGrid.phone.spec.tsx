import { expect, test } from '@playwright/experimental-ct-react';
import { featureBoardTestIds as boardIds } from '../../featureBoard/testIds';
import { touchTestIds } from '../../../touch/testIds';
import { taskboardTestIds as ids } from '../testIds';
import { PRIYA } from './fixtures';
import { PhoneGridHarness } from './PhoneGridHarness';

// The shared Taskboard grid on a phone (SHIP-722): one task state a page, lanes on every page. Runs in the CT phone project.

const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-100/board' } };
const THEMES = ['light', 'dark'] as const;
const STATES = ['t-new', 't-active', 't-closed'];

test('PhoneGrid_WhenParentFeature_ShouldStartEachFeaturesLanesWithItsBandOnEveryPage', async ({
  mount,
  page,
}) => {
  await mount(<PhoneGridHarness board="parent" />, route);

  const pages = page.getByTestId(touchTestIds.pagerPage);
  await expect(pages).toHaveCount(STATES.length);
  for (const state of STATES) {
    const order = await page
      .locator(`[data-testid="${touchTestIds.pagerPage}"][data-page-id="${state}"]`)
      .evaluate(
        (el, t) => {
          const wanted = [t.band, t.lane, t.empty].map((id) => `[data-testid="${id}"]`).join(',');
          return [...el.querySelectorAll<HTMLElement>(wanted)].map((node) =>
            node.dataset.testid === t.band
              ? `band ${node.dataset.section}`
              : (node.dataset.row ?? 'empty'),
          );
        },
        { band: boardIds.band, lane: ids.lane, empty: boardIds.sectionEmpty },
      );
    expect(order, state).toEqual([
      'band SHIP-103',
      'id-SHIP-141',
      'band SHIP-104',
      'id-SHIP-150',
      'band SHIP-105',
      'empty',
    ]);
  }
  await expect(
    page.getByTestId(boardIds.band).first().getByTestId(boardIds.sectionTitle),
  ).toHaveText('Directly under SHIP-103');
});

test('PhoneGrid_WhenLaneCollapsed_ShouldStayCollapsedOnEveryPage', async ({ mount, page }) => {
  await mount(<PhoneGridHarness board="feature" />, route);
  const lanes = page.locator(`[data-testid="${ids.lane}"][data-row="id-SHIP-141"]`);
  await expect(lanes).toHaveCount(STATES.length);

  await lanes.first().getByTestId(ids.rowToggle).tap();

  for (let i = 0; i < STATES.length; i++) {
    await expect(lanes.nth(i)).toHaveAttribute('data-collapsed', 'true');
    await expect(lanes.nth(i).getByTestId(ids.laneTasks)).toHaveCount(0);
  }
  await expect(lanes.nth(1).getByTestId(ids.laneCount)).toHaveText('1');
});

test('PhoneGrid_WhenStoryClosed_ShouldShowOneLine', async ({ mount, page }) => {
  await mount(<PhoneGridHarness board="feature" />, route);

  const lines = page.locator(`[data-testid="${ids.closedLine}"][data-row="id-SHIP-138"]`);
  await expect(lines).toHaveCount(STATES.length);
  await expect(page.locator(`[data-testid="${ids.lane}"][data-row="id-SHIP-138"]`)).toHaveCount(0);
  const first = lines.first();
  await expect(first.getByTestId(ids.rowTitle)).toHaveText('Story SHIP-138');
  await expect(first.getByTestId(ids.closedLineDone)).toHaveText('1/1');
  await expect(first).toContainText('Closed');
  expect((await first.boundingBox())!.height).toBe(40);
  // Closed stories come after every open lane on the page.
  const last = await page
    .locator(`[data-testid="${touchTestIds.pagerPage}"][data-page-id="t-new"]`)
    .evaluate(
      (el, t) => [...el.querySelectorAll<HTMLElement>(t)].map((n) => n.dataset.testid).at(-1),
      `[data-testid="${ids.lane}"],[data-testid="${ids.closedLine}"]`,
    );
  expect(last).toBe(ids.closedLine);
});

test('PhoneGrid_WhenFilterHidesEveryTask_ShouldKeepLaneWithHiddenCount', async ({
  mount,
  page,
}) => {
  await mount(<PhoneGridHarness board="feature" faces={[PRIYA.id]} />, route);

  const newPage = page.locator(`[data-testid="${touchTestIds.pagerPage}"][data-page-id="t-new"]`);
  const lane = newPage.locator(`[data-testid="${ids.lane}"][data-row="id-SHIP-141"]`);
  await expect(lane.getByTestId(ids.rowHeader)).toBeVisible();
  await expect(lane.getByTestId(ids.laneHidden)).toHaveText('1 hidden by Filter');
  await expect(lane.getByTestId(ids.card)).toHaveCount(0);
  await expect(lane.getByTestId(ids.laneNone)).toHaveCount(0);
  await expect(
    page.locator(`[data-testid="${touchTestIds.pagerChip}"][data-page-id="t-new"]`),
  ).toContainText('0');
});

for (const theme of THEMES) {
  for (const [grouping, name] of [
    ['stories', 'Stories'],
    ['people', 'People'],
  ] as const) {
    test(`PhoneGrid_WhenGroupedBy${name}_ShouldMatchItsScreenshot_${theme}`, async ({
      mount,
      page,
    }) => {
      await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
      await mount(<PhoneGridHarness board="feature" grouping={grouping} />, route);
      await expect(page.getByTestId(ids.lane).first()).toBeVisible();
      await expect(page.getByTestId(ids.phoneGrid)).toHaveScreenshot(
        `phone-grid-${grouping}.${theme}.png`,
      );
    });
  }
}
