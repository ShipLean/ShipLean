import { expect, test } from '@playwright/experimental-ct-react';
import { BoardHarness } from '../ct/BoardHarness';
import { LocationProbe } from '../ct/LocationProbe';
import { storiesBoard } from '../ct/fixtures';
import { stubBoard } from '../ct/stubApi';
import { boardTestIds as ids } from '../testIds';

const route = { hooksConfig: { route: '/p/SHIP/boards/stories' } };

test('Card_WhenDraggedByItsTitle_ShouldMoveWithoutOpeningTheModal', async ({ mount, page }) => {
  const server = await stubBoard(page, storiesBoard());
  await mount(
    <>
      <BoardHarness />
      <LocationProbe />
    </>,
    route,
  );
  const card = page
    .getByTestId(ids.card)
    .filter({ has: page.getByTestId(ids.cardKey).filter({ hasText: 'SHIP-160' }) });
  const target = page
    .getByTestId(ids.lane)
    .filter({ has: page.getByTestId(ids.laneName).filter({ hasText: 'Expedite' }) })
    .getByTestId(ids.cell)
    .first();
  await target.scrollIntoViewIfNeeded();
  const from = (await card.getByTestId(ids.cardTitle).boundingBox())!;
  const to = (await target.boundingBox())!;
  const startX = from.x + from.width / 2;
  const startY = from.y + from.height / 2;
  await page.mouse.move(startX, startY);
  await page.mouse.down();
  await page.mouse.move(startX + 6, startY + 6, { steps: 4 });
  await page.mouse.move(to.x + to.width / 2, to.y + to.height / 2, { steps: 12 });
  await page.mouse.up();

  await expect.poll(() => server.moves.length).toBe(1);
  await expect(page.getByTestId(ids.locationProbe)).toHaveText('/p/SHIP/boards/stories');
});
