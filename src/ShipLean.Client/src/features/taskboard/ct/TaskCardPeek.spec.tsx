import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import { TaskboardSlot } from '../../../slots/TaskboardSlot';
import { taskboardTestIds as ids } from '../testIds';
import { AddressReadout } from './AddressReadout';
import { PROJECT, SPRINT, SPRINTS, VIEW } from './fixtures';
import { stubTaskboard } from './stubApi';

const route = { hooksConfig: { route: '/p/SHIP/sprints/s24/taskboard' } };
const slot = (
  <>
    <TaskboardSlot project={PROJECT} sprint={SPRINT} sprints={SPRINTS} />
    <AddressReadout />
  </>
);

const card = (page: Page, key: string) =>
  page
    .getByTestId(ids.card)
    .filter({ has: page.getByTestId(ids.cardKey).filter({ hasText: key }) });

test('TaskCard_WhenTitleClicked_ShouldOpenItemModal', async ({ mount, page }) => {
  await stubTaskboard(page, VIEW);
  await mount(slot, route);

  const title = card(page, 'SHIP-173').getByTestId(ids.cardTitle);
  await expect(title.getByRole('link')).toHaveAttribute('href', '/p/SHIP/items/SHIP-173');
  await title.click();

  await expect(page.getByTestId('address')).toHaveText(
    '/p/SHIP/sprints/s24/taskboard?item=SHIP-173',
  );
});

test('TaskCard_WhenDraggedFromTitle_ShouldMoveTheCardAndNotOpenItemModal', async ({
  mount,
  page,
}) => {
  const server = await stubTaskboard(page, VIEW);
  await mount(slot, route);

  const title = card(page, 'SHIP-173').getByTestId(ids.cardTitle);
  const a = (await title.boundingBox())!;
  const target = (await page
    .getByTestId(ids.row)
    .first()
    .getByTestId(ids.cell)
    .nth(1)
    .boundingBox())!;
  const x = a.x + a.width / 2;
  const y = a.y + a.height / 2;
  await page.mouse.move(x, y);
  await page.mouse.down();
  await page.mouse.move(x + 6, y + 6, { steps: 4 });
  await page.mouse.move(target.x + target.width / 2, target.y + target.height - 8, { steps: 12 });
  await page.mouse.up();

  await expect.poll(() => server.moves.length).toBe(1);
  await expect(page.getByTestId('address')).not.toContainText('item=');
});
