import { expect, test } from '@playwright/experimental-ct-react';
import { componentTestIds as toastIds } from '../../../components/testIds';
import { touchTestIds } from '../../../touch/testIds';
import { BoardHarness } from '../ct/BoardHarness';
import { LocationProbe } from '../ct/LocationProbe';
import type { BoardViewDto } from '@shiplean/core/api/views';
import { CardActionsOpenHarness } from '../ct/CardActionsOpenHarness';
import { card, cell, featuresBoard, lane, storiesBoard } from '../ct/fixtures';
import { stubBoard } from '../ct/stubApi';
import { boardTestIds as ids } from '../testIds';

// Card ⋯, the actions sheet, Move to and Lane, and tap to open, on a phone (SHIP-719, L719c). Runs in the CT phone project.
// Regenerate shots with scripts/update-page-shots.sh src/pages/screenshots/boardCardActions.phone.spec.tsx.

type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];
const route = { hooksConfig: { route: '/p/SHIP/boards/stories' } };
const TOUCH = 32;

const cardKeyed = (page: Page, key: string) =>
  page
    .getByTestId(ids.card)
    .filter({ has: page.getByTestId(ids.cardKey).filter({ hasText: key }) });
const sheet = (page: Page) => page.getByTestId(touchTestIds.sheet);
const row = (page: Page, id: string) =>
  sheet(page).locator(`[data-testid="${touchTestIds.actionSheetRow}"][data-action-id="${id}"]`);
const toasts = (page: Page) => page.getByTestId(toastIds.toastHost).getByTestId(toastIds.toast);

test('Card_WhenTouch_ShouldShowMoreWithoutHover', async ({ mount, page }) => {
  await stubBoard(page, storiesBoard());
  await mount(<BoardHarness />, route);

  const more = cardKeyed(page, 'SHIP-160').getByTestId(touchTestIds.moreButton);
  await expect(more).toBeVisible();
  const box = (await more.boundingBox())!;
  expect(box.width).toBeGreaterThanOrEqual(TOUCH);
  expect(box.height).toBeGreaterThanOrEqual(TOUCH);
  // Nothing hovered or focused: the button is drawn all the same.
  await expect(more).toHaveCSS('opacity', '1');
});

test('CardMore_ShouldShowTheCardHeaderAndWipOverColour', async ({ mount, page }) => {
  await stubBoard(page, storiesBoard());
  await mount(<BoardHarness />, route);

  await cardKeyed(page, 'SHIP-160').getByTestId(touchTestIds.moreButton).tap();
  await expect(sheet(page).getByTestId(touchTestIds.sheetTitle)).toHaveText('SHIP-160 actions');
  const header = sheet(page).getByTestId(ids.cardSheetHeader);
  await expect(header).toContainText('SHIP-160 · New');
  await expect(header).toContainText('Biometric sign-in');
  // One sheet: Move to, Lane and Order in are groups of it, not sheets of their own.
  await expect(sheet(page).getByTestId(touchTestIds.actionSheetGroup)).toHaveText([
    'Move to',
    'Lane',
    'Order in New',
  ]);
  // The column the card is in is ticked "here"; the one over its WIP limit is in the danger tone.
  await expect(row(page, 'column:col-new:false')).toHaveAttribute('aria-current', 'true');
  await expect(row(page, 'column:col-new:false')).toContainText('here');
  await expect(
    row(page, 'column:col-prog:false').getByTestId(touchTestIds.actionSheetDetail),
  ).toHaveText('3/2');
  await expect(row(page, 'column:col-prog:false').getByTestId(ids.cardMoveWip)).toHaveClass(
    /text-alert/,
  );
  await expect(row(page, 'column:col-ready:true')).toContainText('Ready · Done');
  await expect(row(page, 'column:col-new:false')).toBeDisabled();
});

test('CardActionsSheet_WhenColumnPicked_ShouldMoveToEndOfColumn', async ({ mount, page }) => {
  const server = await stubBoard(page, storiesBoard());
  await mount(<BoardHarness />, route);

  await cardKeyed(page, 'SHIP-160').getByTestId(touchTestIds.moreButton).tap();
  await row(page, 'column:col-prog:false').tap();

  await expect.poll(() => server.moves.length).toBe(1);
  expect(server.moves[0]).toEqual({
    itemId: 'id-SHIP-160',
    expectedRevision: 1,
    columnId: 'col-prog',
    done: false,
    laneId: 'lane-default',
    beforeItemId: null,
    method: 'sheet',
  });
  await expect(toasts(page)).toContainText('SHIP-160 → In progress');
  await expect(sheet(page)).toHaveCount(0);
});

test('CardMore_OnAFeatureWithNoPoints_ShouldSitAtTheRight', async ({ mount, page }) => {
  const board = featuresBoard();
  const view: BoardViewDto = {
    ...board,
    lanes: board.lanes.map((l) => ({
      ...l,
      cells: l.cells.map((c) => ({
        ...c,
        cards: c.cards.map((x) => (x.key === 'SHIP-104' ? { ...x, points: null } : x)),
      })),
    })),
  };
  await stubBoard(page, view, { level: 'features' });
  await mount(<BoardHarness level="features" />, {
    hooksConfig: { route: '/p/SHIP/boards/features' },
  });

  const feature = cardKeyed(page, 'SHIP-104');
  const more = (await feature.getByTestId(touchTestIds.moreButton).boundingBox())!;
  const box = (await feature.boundingBox())!;
  // The warn ? shows and the ⋯ is at the card's right edge (inside its padding), not beside the key.
  await expect(feature.getByTestId(ids.cardPoints)).toHaveText('?');
  expect(more.x + more.width).toBeGreaterThan(box.x + box.width - 12);
  // The ⋯'s wrapper is a real box (a `display: contents` wrapper has none), so a `display: contents` wrapper goes red here. The ⋯ itself reaches the right only because the "?" chip carries ml-auto; a spacer in Card.tsx is the real fix.
  const wrapper = await feature.getByTestId(ids.cardMore).boundingBox();
  expect(wrapper?.width ?? 0).toBeGreaterThan(0);
});

test('CardActionsSheet_WhenLanePicked_ShouldMoveToLane', async ({ mount, page }) => {
  const server = await stubBoard(page, storiesBoard());
  await mount(<BoardHarness />, route);

  await cardKeyed(page, 'SHIP-160').getByTestId(touchTestIds.moreButton).tap();
  await expect(row(page, 'lane:lane-default')).toContainText('here');
  await row(page, 'lane:lane-expedite').tap();

  await expect.poll(() => server.moves.length).toBe(1);
  expect(server.moves[0]).toMatchObject({
    itemId: 'id-SHIP-160',
    columnId: 'col-new',
    done: false,
    laneId: 'lane-expedite',
    beforeItemId: null,
  });
});

test('CardActionsSheet_WhenTopAndBottom_ShouldDisableAtTheEdges', async ({ mount, page }) => {
  await stubBoard(page, storiesBoard());
  await mount(<BoardHarness />, route);

  // SHIP-160 is alone in its cell: it is already first and last.
  await cardKeyed(page, 'SHIP-160').getByTestId(touchTestIds.moreButton).tap();
  await expect(row(page, 'top')).toBeDisabled();
  await expect(row(page, 'bottom')).toBeDisabled();
});

test('CardActionsSheet_WhenFeature_ShouldOfferWhatIsUnderIt', async ({ mount, page }) => {
  await stubBoard(page, featuresBoard(), { level: 'features' });
  await mount(<BoardHarness level="features" />, {
    hooksConfig: { route: '/p/SHIP/boards/features' },
  });

  await cardKeyed(page, 'SHIP-104').getByTestId(touchTestIds.moreButton).tap();
  await expect(row(page, 'below')).toContainText("Show what's under it");
});

test('Card_WhenTappedOnPhone_ShouldOpenItemModal', async ({ mount, page }) => {
  await stubBoard(page, storiesBoard());
  await mount(
    <>
      <BoardHarness />
      <LocationProbe />
    </>,
    route,
  );

  // On the key, outside the title link and the ⋯ button.
  await cardKeyed(page, 'SHIP-160').getByTestId(ids.cardKey).tap();
  await expect(page.getByTestId(ids.locationProbe)).toHaveText(
    '/p/SHIP/boards/stories?item=SHIP-160',
  );
});

test('Card_WhenMoreTapped_ShouldNotOpenItemModal', async ({ mount, page }) => {
  await stubBoard(page, storiesBoard());
  await mount(
    <>
      <BoardHarness />
      <LocationProbe />
    </>,
    route,
  );

  await cardKeyed(page, 'SHIP-160').getByTestId(touchTestIds.moreButton).tap();
  await page.getByTestId(touchTestIds.sheetScrim).tap({ position: { x: 5, y: 5 } });
  await expect(sheet(page)).toHaveCount(0);
  await expect(page.getByTestId(ids.locationProbe)).toHaveText('/p/SHIP/boards/stories');
});

function boardWithTwoNew(): BoardViewDto {
  const base = storiesBoard();
  return {
    ...base,
    lanes: base.lanes.map((candidate) =>
      candidate.id !== 'lane-default'
        ? candidate
        : lane({
            ...candidate,
            cells: [
              cell('col-new', [card({ key: 'SHIP-163' }), card({ key: 'SHIP-164' })]),
              ...candidate.cells.slice(1),
            ],
          }),
    ),
  };
}

test('CardActionsSheet_WhenMoveToTopPicked_ShouldSendItAboveTheFirstCard', async ({
  mount,
  page,
}) => {
  const server = await stubBoard(page, boardWithTwoNew());
  await mount(<BoardHarness />, route);

  await cardKeyed(page, 'SHIP-164').getByTestId(touchTestIds.moreButton).tap();
  await expect(row(page, 'bottom')).toBeDisabled();
  await row(page, 'top').tap();

  await expect.poll(() => server.moves.length).toBe(1);
  expect(server.moves[0]).toMatchObject({
    itemId: 'id-SHIP-164',
    columnId: 'col-new',
    laneId: 'lane-default',
    beforeItemId: 'id-SHIP-163',
  });
});

test('CardActionsSheet_WhenMoveToBottomPicked_ShouldSendItToTheEnd', async ({ mount, page }) => {
  const server = await stubBoard(page, boardWithTwoNew());
  await mount(<BoardHarness />, route);

  await cardKeyed(page, 'SHIP-163').getByTestId(touchTestIds.moreButton).tap();
  await expect(row(page, 'top')).toBeDisabled();
  await row(page, 'bottom').tap();

  await expect.poll(() => server.moves.length).toBe(1);
  expect(server.moves[0]).toMatchObject({
    itemId: 'id-SHIP-163',
    columnId: 'col-new',
    laneId: 'lane-default',
    beforeItemId: null,
  });
});

test('CardActionsSheet_WhenOpenedByHook_ShouldOfferWhatIsUnderIt', async ({ mount, page }) => {
  await mount(<CardActionsOpenHarness view={featuresBoard()} cardId="id-SHIP-104" />, route);

  await page.getByTestId('harness-open').tap();
  await expect(sheet(page).getByTestId(touchTestIds.sheetTitle)).toHaveText('SHIP-104 actions');
  await row(page, 'below').tap();

  await expect(page.getByTestId('harness-called')).toHaveText('id-SHIP-104 SHIP-104 story');
});
