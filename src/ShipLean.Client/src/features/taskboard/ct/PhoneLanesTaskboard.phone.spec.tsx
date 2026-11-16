import { expect, test } from '@playwright/experimental-ct-react';
import type { TaskboardViewDto } from '@shiplean/core/api/views';
import { touchTestIds } from '../../../touch/testIds';
import { TaskboardSlot } from '../../../slots/TaskboardSlot';
import { taskboardTestIds as ids } from '../testIds';
import { PROJECT, SPRINT, SPRINTS, VIEW } from './fixtures';
import { stubTaskboard } from './stubApi';

// The sprint Taskboard's Unparented lane and "All tasks done" offer on a phone (SHIP-725 D12, D14). Runs in the CT phone project.

const route = { hooksConfig: { route: '/p/SHIP/sprints/s24/taskboard' } };
const slot = <TaskboardSlot project={PROJECT} sprint={SPRINT} sprints={SPRINTS} />;
const THEMES = ['light', 'dark'] as const;
const PAGES = 3;

// The Unparented row with nothing in it.
const EMPTY_UNPARENTED: TaskboardViewDto = {
  ...VIEW,
  rows: VIEW.rows.map((row) =>
    row.parent === null ? { ...row, cells: row.cells.map((c) => ({ ...c, tasks: [] })) } : row,
  ),
};
// The open story's tasks are all closed: its lane offers Move to Resolved (its type has a Resolved state to move to).
const ALL_DONE: TaskboardViewDto = {
  ...VIEW,
  parentStates: VIEW.parentStates.map((types) => ({
    ...types,
    states: [
      ...types.states,
      { id: 'ps-resolved', name: 'Resolved', category: 'resolved' as const },
    ],
  })),
  rows: VIEW.rows.map((row, i) => (i === 0 ? { ...row, offerResolve: true } : row)),
};

const unparentedLanes = (page: Parameters<Parameters<typeof test>[2]>[0]['page']) =>
  page.locator(`[data-testid="${ids.lane}"][data-row="unparented"]`);

test('PhoneLanes_WhenNoUnparentedTasks_ShouldStillShowEmptyUnparentedLane', async ({
  mount,
  page,
}) => {
  await stubTaskboard(page, EMPTY_UNPARENTED, { holdLaterReads: true });
  await mount(slot, route);

  const lanes = unparentedLanes(page);
  await expect(lanes).toHaveCount(PAGES);
  for (let i = 0; i < PAGES; i++) {
    const lane = lanes.nth(i);
    await expect(lane.getByTestId(ids.laneNone)).toHaveText('None');
    await expect(lane.locator('[data-drop-target]')).toHaveCount(1);
    await expect(lane.getByTestId(ids.rowCount)).toHaveText('0 tasks · no points');
    // The last lane of its page.
    await expect(
      lane
        .locator('xpath=ancestor::*[@data-testid="' + touchTestIds.pagerPage + '"]')
        .getByTestId(ids.lane)
        .last(),
    ).toHaveAttribute('data-row', 'unparented');
  }
});

test('PhoneLanes_WhenTaskAddedInUnparented_ShouldCreateItWithoutParent', async ({
  mount,
  page,
}) => {
  const server = await stubTaskboard(page, VIEW, { holdLaterReads: true });
  await mount(slot, route);

  const lane = unparentedLanes(page).first();
  await lane.getByTestId(ids.addTask).tap();
  await lane.getByTestId(ids.taskTitle).fill('Loose end');
  await lane.getByTestId(ids.taskTitle).press('Enter');

  await expect.poll(() => server.creates.length).toBe(1);
  expect(server.creates[0]).toMatchObject({ type: 'task', title: 'Loose end', parentId: null });
});

test('PhoneLanes_WhenOfferDismissed_ShouldHideItOnEveryPage', async ({ mount, page }) => {
  await stubTaskboard(page, ALL_DONE, { holdLaterReads: true });
  await mount(slot, route);

  const offers = page.getByTestId(ids.rowOffer);
  await expect(offers).toHaveCount(PAGES);
  await expect(offers.first().getByTestId(ids.rowOfferResolve)).toHaveText('Move to Resolved');
  expect((await offers.first().getByTestId(ids.rowOfferDismiss).boundingBox())!.height).toBe(32);

  await offers.first().getByTestId(ids.rowOfferDismiss).tap();

  await expect(offers).toHaveCount(0);
});

for (const theme of THEMES) {
  test(`PhoneLanes_WhenUnparentedShown_ShouldMatchTheScreenshot_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubTaskboard(page, VIEW, { holdLaterReads: true });
    await mount(slot, route);

    const lane = unparentedLanes(page).first();
    await expect(lane.getByTestId(ids.rowCount)).toHaveText('1 task · no points');
    await expect(lane).toHaveScreenshot(`taskboard-unparented-phone-${theme}.png`);
  });
}

for (const theme of THEMES) {
  test(`PhoneLanes_WhenOfferShownOnTheClosedPage_ShouldMatchTheScreenshot_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubTaskboard(page, ALL_DONE, { holdLaterReads: true });
    await mount(slot, route);

    const closed = page.locator(`[data-testid="${touchTestIds.pagerChip}"]`).filter({
      hasText: /^\s*Closed(?![A-Za-z])/,
    });
    await closed.tap();
    await expect(closed).toHaveAttribute('aria-current', 'true');
    const closedPage = page.locator(
      `[data-testid="${touchTestIds.pagerPage}"][data-page-id="t-closed"]`,
    );
    const offer = closedPage.getByTestId(ids.rowOffer);
    // The smooth scroll has settled once the offer is wholly on screen.
    await expect(offer).toBeInViewport({ ratio: 1 });
    await expect(page).toHaveScreenshot(`taskboard-alldone-phone.${theme}.png`);
  });
}
