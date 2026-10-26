import { expect, test } from '@playwright/experimental-ct-react';
import type { Locator, Page } from 'playwright';
import { moveRowToSection } from '../drag/useReorder';
import { PROJECT, planningView } from '../ct/fixtures';
import { stubBacklog } from '../ct/stubApi';
import { backlogTestIds as ids } from '../testIds';
import { PlanningMode } from './PlanningMode';

const route = { hooksConfig: { route: '/p/SHIP/backlogs/stories?mode=planning' } };
const rowOf = (page: Page, key: string) =>
  page.getByTestId(ids.row).and(page.locator(`[data-key="${key}"]`));
const keysInOrder = (page: Page) =>
  page.getByTestId(ids.row).evaluateAll((els) => els.map((e) => e.getAttribute('data-key')));

// Picks `row` up by its grip and holds the pointer over `target`, a little above or below its middle.
async function holdOver(page: Page, row: Locator, target: Locator, where: 'above' | 'below') {
  const grip = (await row.getByTestId(ids.grip).boundingBox())!;
  const box = (await target.boundingBox())!;
  const x = grip.x + grip.width / 2;
  const y = grip.y + grip.height / 2;
  await page.mouse.move(x, y);
  await page.mouse.down();
  await page.mouse.move(x + 5, y + 10, { steps: 4 });
  await page.mouse.move(
    box.x + box.width / 2,
    box.y + box.height / 2 + (where === 'above' ? -12 : 12),
    {
      steps: 12,
    },
  );
}

// Sprint 24: SHIP-160. Sprint 25: SHIP-158. Sprint 26: empty. Backlog: SHIP-170, SHIP-171, SHIP-172.
test('Planning_WhenDraggedOverARow_ShouldDrawTheLineOnTheEdgeItLandsAndSlideNothing', async ({
  mount,
  page,
}) => {
  const view = planningView();
  const server = await stubBacklog(page, view);
  await mount(<PlanningMode project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(5);

  await holdOver(page, rowOf(page, 'SHIP-170'), rowOf(page, 'SHIP-172'), 'above');
  await expect(rowOf(page, 'SHIP-172')).toHaveAttribute('data-edge', 'top');
  // Nothing slides to make room: the line is the only preview.
  for (const key of ['SHIP-158', 'SHIP-160', 'SHIP-171', 'SHIP-172']) {
    await expect(rowOf(page, key)).not.toHaveAttribute('style', /translate/);
  }
  // What the server reads back once the move is made.
  server.setBacklog(moveRowToSection(view, 'id-SHIP-170', 'backlog', 'id-SHIP-172'));
  await page.mouse.up();

  // What was drawn is what is sent: just above SHIP-172, and that is where the row shows.
  await expect.poll(() => server.reorders.length).toBe(1);
  expect(server.reorders[0].body).toMatchObject({ beforeItemId: 'id-SHIP-172' });
  expect(await keysInOrder(page)).toEqual([
    'SHIP-160',
    'SHIP-158',
    'SHIP-171',
    'SHIP-170',
    'SHIP-172',
  ]);
});

test('Planning_WhenPointerMovesToTheLowerHalf_ShouldDrawTheLineOnTheBottomEdge', async ({
  mount,
  page,
}) => {
  const view = planningView();
  const server = await stubBacklog(page, view);
  await mount(<PlanningMode project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(5);

  await holdOver(page, rowOf(page, 'SHIP-170'), rowOf(page, 'SHIP-171'), 'below');
  await expect(rowOf(page, 'SHIP-171')).toHaveAttribute('data-edge', 'bottom');
  // What the server reads back once the move is made.
  server.setBacklog(moveRowToSection(view, 'id-SHIP-170', 'backlog', 'id-SHIP-172'));
  await page.mouse.up();

  await expect.poll(() => server.reorders.length).toBe(1);
  expect(server.reorders[0].body).toMatchObject({ beforeItemId: 'id-SHIP-172' });
  expect(await keysInOrder(page)).toEqual([
    'SHIP-160',
    'SHIP-158',
    'SHIP-171',
    'SHIP-170',
    'SHIP-172',
  ]);
});

test('Planning_WhenDraggedToAnotherSection_ShouldDrawTheLineThereAndLeaveOtherSectionsAlone', async ({
  mount,
  page,
}) => {
  const view = planningView();
  const server = await stubBacklog(page, view);
  await mount(<PlanningMode project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(5);

  await holdOver(page, rowOf(page, 'SHIP-160'), rowOf(page, 'SHIP-171'), 'above');
  await expect(rowOf(page, 'SHIP-171')).toHaveAttribute('data-edge', 'top');
  for (const key of ['SHIP-158', 'SHIP-170', 'SHIP-171', 'SHIP-172']) {
    await expect(rowOf(page, key)).not.toHaveAttribute('style', /translate/);
  }
  // What the server reads back once the move is made.
  server.setBacklog(moveRowToSection(view, 'id-SHIP-160', 'backlog', 'id-SHIP-171'));
  await page.mouse.up();

  await expect.poll(() => server.reorders.length).toBe(1);
  expect(server.reorders[0].body).toMatchObject({ beforeItemId: 'id-SHIP-171' });
  await expect(rowOf(page, 'SHIP-160')).toHaveAttribute('data-section', 'backlog');
  expect(await keysInOrder(page)).toEqual([
    'SHIP-158',
    'SHIP-170',
    'SHIP-160',
    'SHIP-171',
    'SHIP-172',
  ]);
});

test('Planning_WhenDropped_ShouldRemoveTheLine', async ({ mount, page }) => {
  await stubBacklog(page, planningView());
  await mount(<PlanningMode project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(5);

  await holdOver(page, rowOf(page, 'SHIP-170'), rowOf(page, 'SHIP-172'), 'above');
  await expect(rowOf(page, 'SHIP-172')).toHaveAttribute('data-over', 'true');
  await page.mouse.up();

  await expect(page.locator('[data-over="true"]')).toHaveCount(0);
});
