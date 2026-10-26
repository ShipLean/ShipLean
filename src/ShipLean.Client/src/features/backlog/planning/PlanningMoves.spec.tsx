import { expect, test } from '@playwright/experimental-ct-react';
import { ToastHost } from '../../../components/ToastHost';
import { componentTestIds } from '../../../components/testIds';
import { moveRowToSection } from '../drag/useReorder';
import { dragRow } from '../ct/dragRow';
import { PROJECT, planningSprints, planningView } from '../ct/fixtures';
import { stubBacklog } from '../ct/stubApi';
import { backlogTestIds as ids } from '../testIds';
import { PlanningMode } from './PlanningMode';

const route = { hooksConfig: { route: '/p/SHIP/backlogs/stories?mode=planning' } };

// Sprint 24: SHIP-160 (13 pts). Sprint 25: SHIP-158. Sprint 26: empty. Backlog: SHIP-170, SHIP-171, SHIP-172.
const mountPlanning = async (mount: (c: React.JSX.Element, o: typeof route) => Promise<unknown>) =>
  mount(
    <>
      <PlanningMode project={PROJECT} />
      <ToastHost />
    </>,
    route,
  );

const patched = { id: 'x', key: 'x', revision: 5, changes: [], notices: [] };
const sprintChange = (value: string | null) => expect.objectContaining({ sprint: { value } });

test('Planning_WhenRowDroppedOnARowOfAnotherSection_ShouldSetSprintThenOrderOnTheNewRevision', async ({
  mount,
  page,
}) => {
  const server = await stubBacklog(page, planningView(), {
    patchAnswers: [{ status: 200, body: patched }],
  });
  await mountPlanning(mount);
  const row = (key: string) => page.getByTestId(ids.row).and(page.locator(`[data-key="${key}"]`));
  await expect(page.getByTestId(ids.row)).toHaveCount(5);

  await dragRow(page, row('SHIP-160'), row('SHIP-171'), 'above');

  await expect.poll(() => server.reorders.length).toBe(1);
  expect(server.patches).toHaveLength(1);
  expect(server.patches[0].url).toContain('/api/work-items/id-SHIP-160');
  expect(server.patches[0].body).toEqual({
    expectedRevision: 1,
    changes: sprintChange(null),
  });
  expect(server.reorders[0].url).toContain('/api/work-items/id-SHIP-160/reorder');
  expect(server.reorders[0].body).toEqual({ expectedRevision: 5, beforeItemId: 'id-SHIP-171' });
  await expect(page.getByTestId(componentTestIds.toast)).toContainText('SHIP-160 → Backlog');
});

test('Planning_WhenRowDroppedBelowTheLastRowOfASprint_ShouldSendSprintAndPutItLast', async ({
  mount,
  page,
}) => {
  const server = await stubBacklog(page, planningView());
  await mountPlanning(mount);
  const row = (key: string) => page.getByTestId(ids.row).and(page.locator(`[data-key="${key}"]`));
  await expect(page.getByTestId(ids.row)).toHaveCount(5);

  await dragRow(page, row('SHIP-170'), row('SHIP-158'), 'below');

  await expect.poll(() => server.reorders.length).toBe(1);
  expect(server.patches[0].body).toEqual({ expectedRevision: 1, changes: sprintChange('sp25') });
  expect(server.reorders[0].body).toEqual({ expectedRevision: 5, beforeItemId: null });
});

test('Planning_WhenRowDroppedInItsOwnSection_ShouldReorderWithoutAPatch', async ({
  mount,
  page,
}) => {
  const server = await stubBacklog(page, planningView());
  await mountPlanning(mount);
  const row = (key: string) => page.getByTestId(ids.row).and(page.locator(`[data-key="${key}"]`));
  await expect(page.getByTestId(ids.row)).toHaveCount(5);

  await dragRow(page, row('SHIP-170'), row('SHIP-172'), 'below');

  await expect.poll(() => server.reorders.length).toBe(1);
  expect(server.patches).toHaveLength(0);
  expect(server.reorders[0].body).toEqual({ expectedRevision: 1, beforeItemId: null });
  await expect(page.getByTestId(componentTestIds.toast)).toContainText('SHIP-170 reordered');
});

test('Planning_WhenRowDroppedOnASectionHeader_ShouldChangeTheSprintAndSendNoReorder', async ({
  mount,
  page,
}) => {
  const server = await stubBacklog(page, planningView());
  await mountPlanning(mount);
  const row = (key: string) => page.getByTestId(ids.row).and(page.locator(`[data-key="${key}"]`));
  await expect(page.getByTestId(ids.row)).toHaveCount(5);

  await dragRow(page, row('SHIP-170'), page.getByTestId(ids.planningSection).nth(1));

  await expect.poll(() => server.patches.length).toBe(1);
  expect(server.patches[0].body).toEqual({ expectedRevision: 1, changes: sprintChange('sp25') });
  await expect(page.getByTestId(componentTestIds.toast)).toContainText('SHIP-170 → Sprint 25');
  expect(server.reorders).toHaveLength(0);
});

test('Planning_WhenRowDroppedOnAnEmptySprint_ShouldChangeTheSprintOnly', async ({
  mount,
  page,
}) => {
  const server = await stubBacklog(page, planningView());
  await mountPlanning(mount);
  const row = (key: string) => page.getByTestId(ids.row).and(page.locator(`[data-key="${key}"]`));
  await expect(page.getByTestId(ids.row)).toHaveCount(5);

  await dragRow(page, row('SHIP-170'), page.getByTestId(ids.planningEmptySection));

  await expect.poll(() => server.patches.length).toBe(1);
  expect(server.patches[0].body).toEqual({ expectedRevision: 1, changes: sprintChange('sp26') });
  expect(server.reorders).toHaveLength(0);
});

test('Planning_WhenRowDroppedOnTheHeaderOfAnEmptySprint_ShouldChangeTheSprintOnly', async ({
  mount,
  page,
}) => {
  const server = await stubBacklog(page, planningView());
  await mountPlanning(mount);
  const row = (key: string) => page.getByTestId(ids.row).and(page.locator(`[data-key="${key}"]`));
  await expect(page.getByTestId(ids.row)).toHaveCount(5);

  await dragRow(page, row('SHIP-170'), page.getByTestId(ids.planningSection).nth(2));

  await expect.poll(() => server.patches.length).toBe(1);
  expect(server.patches[0].body).toEqual({ expectedRevision: 1, changes: sprintChange('sp26') });
  expect(server.reorders).toHaveLength(0);
});

test('Planning_WhenRowDroppedOnItsOwnHeader_ShouldSendNothing', async ({ mount, page }) => {
  const server = await stubBacklog(page, planningView());
  await mountPlanning(mount);
  const row = (key: string) => page.getByTestId(ids.row).and(page.locator(`[data-key="${key}"]`));
  await expect(page.getByTestId(ids.row)).toHaveCount(5);

  await dragRow(page, row('SHIP-170'), page.getByTestId(ids.planningSection).nth(3));

  await expect(page.getByTestId(componentTestIds.toast)).toHaveCount(0);
  expect(server.patches).toHaveLength(0);
  expect(server.reorders).toHaveLength(0);
});

test('Planning_WhenRowDropped_ShouldMoveItIntoTheSectionAtOnce', async ({ mount, page }) => {
  await stubBacklog(page, planningView());
  // The PATCH is never answered: what the page shows is the move it made itself.
  await page.route('**/api/work-items/*', () => new Promise(() => undefined));
  await mountPlanning(mount);
  const row = (key: string) => page.getByTestId(ids.row).and(page.locator(`[data-key="${key}"]`));
  await expect(page.getByTestId(ids.row)).toHaveCount(5);

  await dragRow(page, row('SHIP-170'), page.getByTestId(ids.planningSection).nth(1));

  await expect(row('SHIP-170')).toHaveAttribute('data-section', 'sp25');
  await expect(page.getByTestId(ids.planningSectionCount).nth(1)).toHaveText('2 items');
  await expect(page.getByTestId(ids.planningCommitted).nth(1)).toHaveText('13 / 22 pts');
});

test('Planning_WhenMovedTwiceBeforeTheBacklogIsReadAgain_ShouldSendTheRevisionTheServerReturned', async ({
  mount,
  page,
}) => {
  const server = await stubBacklog(page, planningView());
  // The server holds SHIP-170 at 1 and bumps it with each sprint change; a PATCH at another revision is a 409.
  let revision = 1;
  await page.route('**/api/work-items/*', async (r) => {
    const body = r.request().postDataJSON() as { expectedRevision: number };
    server.patches.push({ url: r.request().url(), body });
    if (body.expectedRevision !== revision) {
      await r.fulfill({
        status: 409,
        contentType: 'application/json',
        body: JSON.stringify({
          error: 'Changed by someone else',
          code: 'conflict',
          current: { revision: 99, changedBy: null },
          theirChanges: [],
        }),
      });
      return;
    }
    revision += 1;
    await r.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ ...patched, revision }),
    });
  });
  await mountPlanning(mount);
  const row = (key: string) => page.getByTestId(ids.row).and(page.locator(`[data-key="${key}"]`));
  await expect(page.getByTestId(ids.row)).toHaveCount(5);
  // The reads that follow each move are held, so the cache is all the page has.
  await page.route('**/api/projects/p1/backlog?*', () => new Promise(() => undefined));

  await dragRow(page, row('SHIP-170'), page.getByTestId(ids.planningSection).nth(1));
  await expect.poll(() => server.patches.length).toBe(1);
  await expect(row('SHIP-170')).toHaveAttribute('data-section', 'sp25');
  await dragRow(page, row('SHIP-170'), page.getByTestId(ids.planningSection).nth(3));

  await expect.poll(() => server.patches.length).toBe(2);
  expect(
    server.patches.map((p) => (p.body as { expectedRevision: number }).expectedRevision),
  ).toEqual([1, 2]);
  await expect(page.getByTestId(ids.conflict)).toHaveCount(0);
});

test('Planning_WhenAltArrowDownOnTheLastRowOfASprint_ShouldMoveItIntoTheNextSprint', async ({
  mount,
  page,
}) => {
  const view = planningView();
  const server = await stubBacklog(page, view);
  await mountPlanning(mount);
  const row = (key: string) => page.getByTestId(ids.row).and(page.locator(`[data-key="${key}"]`));
  await expect(page.getByTestId(ids.row)).toHaveCount(5);
  // What the server will read back once the move is made.
  server.setBacklog(moveRowToSection(view, 'id-SHIP-160', 'sp25', 'id-SHIP-158'));

  await row('SHIP-160').focus();
  await page.keyboard.press('Alt+ArrowDown');

  await expect.poll(() => server.reorders.length).toBe(1);
  expect(server.patches[0].body).toEqual({ expectedRevision: 1, changes: sprintChange('sp25') });
  expect(server.reorders[0].body).toEqual({ expectedRevision: 5, beforeItemId: 'id-SHIP-158' });
  // The row is rebuilt in its new section and keeps the focus, so the next press carries on.
  await expect(row('SHIP-160')).toHaveAttribute('data-section', 'sp25');
  await expect(row('SHIP-160')).toBeFocused();
});

test('Planning_WhenAltArrowDownPressedTwiceWhileTheFirstWriteIsInFlight_ShouldSendTheSecondOnTheRevisionTheFirstLeft', async ({
  mount,
  page,
}) => {
  let release!: () => void;
  const held = new Promise<void>((resolve) => {
    release = resolve;
  });
  const server = await stubBacklog(page, planningView(), {
    holdFirstPatch: held,
    patchAnswers: [
      { status: 200, body: { ...patched, revision: 2 } },
      { status: 200, body: { ...patched, revision: 3 } },
    ],
  });
  await mountPlanning(mount);
  const row = (key: string) => page.getByTestId(ids.row).and(page.locator(`[data-key="${key}"]`));
  await expect(page.getByTestId(ids.row)).toHaveCount(5);

  // Sprint 25's only item goes down into the empty Sprint 26, then on into the Backlog above SHIP-170.
  await row('SHIP-158').focus();
  await page.keyboard.press('Alt+ArrowDown');
  await expect(row('SHIP-158')).toHaveAttribute('data-section', 'sp26');
  await expect(row('SHIP-158')).toBeFocused();
  await page.keyboard.press('Alt+ArrowDown');
  await expect(row('SHIP-158')).toHaveAttribute('data-section', 'backlog');
  // Only the first write has been sent; the second waits for it.
  expect(server.patches).toHaveLength(1);
  release();

  await expect.poll(() => server.patches.length).toBe(2);
  expect(
    server.patches.map((p) => (p.body as { expectedRevision: number }).expectedRevision),
  ).toEqual([1, 2]);
  await expect.poll(() => server.reorders.length).toBe(1);
  expect(server.reorders[0].body).toEqual({ expectedRevision: 3, beforeItemId: 'id-SHIP-170' });
  await expect(page.getByTestId(componentTestIds.conflictBanner)).toHaveCount(0);
});

test('Planning_WhenAltArrowUpInsideASection_ShouldMoveOnePlaceWithoutChangingTheSprint', async ({
  mount,
  page,
}) => {
  const server = await stubBacklog(page, planningView());
  await mountPlanning(mount);
  const row = (key: string) => page.getByTestId(ids.row).and(page.locator(`[data-key="${key}"]`));
  await expect(page.getByTestId(ids.row)).toHaveCount(5);

  await row('SHIP-172').focus();
  await page.keyboard.press('Alt+ArrowUp');

  await expect.poll(() => server.reorders.length).toBe(1);
  expect(server.patches).toHaveLength(0);
  expect(server.reorders[0].body).toEqual({ expectedRevision: 1, beforeItemId: 'id-SHIP-171' });
});

test('Planning_WhenAltArrowUpOnTheFirstRowOfTheBacklog_ShouldPutItLastInTheSprintAbove', async ({
  mount,
  page,
}) => {
  const server = await stubBacklog(page, planningView());
  await mountPlanning(mount);
  const row = (key: string) => page.getByTestId(ids.row).and(page.locator(`[data-key="${key}"]`));
  await expect(page.getByTestId(ids.row)).toHaveCount(5);

  // Sprint 26 sits above the Backlog and has no rows: the sprint changes, no place is named.
  await row('SHIP-170').focus();
  await page.keyboard.press('Alt+ArrowUp');

  await expect.poll(() => server.patches.length).toBe(1);
  expect(server.patches[0].body).toEqual({ expectedRevision: 1, changes: sprintChange('sp26') });
  expect(server.reorders).toHaveLength(0);
});

const staleBody = {
  error: 'This item changed.',
  code: 'conflict',
  current: { id: 'id-SHIP-170', revision: 9, changedBy: null },
  theirChanges: [{ rev: 9, changedBy: { name: 'Tom Okafor' } }],
};

test('Planning_WhenTheSprintChangeIsStale_ShouldOfferUseMineThatResendsBothWritesOnTheNewRevision', async ({
  mount,
  page,
}) => {
  const server = await stubBacklog(page, planningView(), {
    patchAnswers: [
      { status: 409, body: staleBody },
      { status: 200, body: { ...patched, revision: 10 } },
    ],
  });
  await mountPlanning(mount);
  const row = (key: string) => page.getByTestId(ids.row).and(page.locator(`[data-key="${key}"]`));
  await expect(page.getByTestId(ids.row)).toHaveCount(5);

  await dragRow(page, row('SHIP-170'), row('SHIP-158'), 'above');
  await expect(page.getByTestId(componentTestIds.conflictBanner)).toContainText('Tom Okafor');
  expect(server.reorders).toHaveLength(0);
  await page.getByTestId(componentTestIds.conflictUseMine).click();

  await expect(page.getByTestId(componentTestIds.conflictBanner)).toHaveCount(0);
  expect(server.patches[1].body).toEqual({ expectedRevision: 9, changes: sprintChange('sp25') });
  expect(server.reorders).toHaveLength(1);
  expect(server.reorders[0].body).toEqual({ expectedRevision: 10, beforeItemId: 'id-SHIP-158' });
});

test('Planning_WhenTheReorderStepIsStale_ShouldResendOnlyTheReorder', async ({ mount, page }) => {
  const server = await stubBacklog(page, planningView(), {
    patchAnswers: [{ status: 200, body: { ...patched, revision: 5 } }],
    reorderAnswers: [
      { status: 409, body: staleBody },
      { status: 200, body: { ...patched, revision: 11 } },
    ],
  });
  await mountPlanning(mount);
  const row = (key: string) => page.getByTestId(ids.row).and(page.locator(`[data-key="${key}"]`));
  await expect(page.getByTestId(ids.row)).toHaveCount(5);

  await dragRow(page, row('SHIP-170'), row('SHIP-158'), 'above');
  await expect(page.getByTestId(componentTestIds.conflictBanner)).toContainText('Tom Okafor');
  expect(server.patches).toHaveLength(1);
  await page.getByTestId(componentTestIds.conflictUseMine).click();

  await expect(page.getByTestId(componentTestIds.conflictBanner)).toHaveCount(0);
  expect(server.reorders).toHaveLength(2);
  expect(server.reorders[1].body).toEqual({ expectedRevision: 9, beforeItemId: 'id-SHIP-158' });
  // The sprint was changed once: teammates see one change, not two.
  expect(server.patches).toHaveLength(1);
});

test('Planning_WhenTheReorderStepFails_ShouldSayTheItemMovedButItsPlaceWasNotSaved', async ({
  mount,
  page,
}) => {
  const server = await stubBacklog(page, planningView(), {
    reorderAnswers: [{ status: 500, body: { error: 'Server error.', code: 'internal' } }],
  });
  await mountPlanning(mount);
  const row = (key: string) => page.getByTestId(ids.row).and(page.locator(`[data-key="${key}"]`));
  await expect(page.getByTestId(ids.row)).toHaveCount(5);

  await dragRow(page, row('SHIP-170'), row('SHIP-158'), 'above');

  await expect(page.getByTestId(componentTestIds.toast)).toContainText(
    'SHIP-170 moved to Sprint 25; its place was not saved',
  );
  expect(server.patches).toHaveLength(1);
  await expect(page.getByTestId(componentTestIds.conflictBanner)).toHaveCount(0);
});

test('Planning_WhenALaterMoveSucceeds_ShouldKeepAnEarlierMovesConflictBanner', async ({
  mount,
  page,
}) => {
  const server = await stubBacklog(page, planningView(), {
    patchAnswers: [
      { status: 409, body: staleBody },
      { status: 200, body: { ...patched, revision: 6 } },
    ],
  });
  await mountPlanning(mount);
  const row = (key: string) => page.getByTestId(ids.row).and(page.locator(`[data-key="${key}"]`));
  await expect(page.getByTestId(ids.row)).toHaveCount(5);

  await dragRow(page, row('SHIP-170'), row('SHIP-158'), 'above');
  await expect(page.getByTestId(componentTestIds.conflictBanner)).toContainText('SHIP-170');
  await dragRow(page, row('SHIP-172'), page.getByTestId(ids.planningSection).nth(1));

  await expect.poll(() => server.patches.length).toBe(2);
  await expect(page.getByTestId(componentTestIds.toast)).toContainText('SHIP-172 → Sprint 25');
  await expect(page.getByTestId(componentTestIds.conflictBanner)).toContainText('SHIP-170');
});

test('Planning_WhenTheSprintChangeIsStale_ShouldKeepTheirsByReadingTheBacklogAgain', async ({
  mount,
  page,
}) => {
  const server = await stubBacklog(page, planningView(), {
    patchAnswers: [{ status: 409, body: staleBody }],
  });
  await mountPlanning(mount);
  const row = (key: string) => page.getByTestId(ids.row).and(page.locator(`[data-key="${key}"]`));
  await expect(page.getByTestId(ids.row)).toHaveCount(5);
  await dragRow(page, row('SHIP-170'), row('SHIP-158'), 'above');
  await expect(page.getByTestId(componentTestIds.conflictBanner)).toBeVisible();
  const readsBefore = server.reads.length;

  await page.getByTestId(componentTestIds.conflictKeepTheirs).click();

  await expect(page.getByTestId(componentTestIds.conflictBanner)).toHaveCount(0);
  await expect.poll(() => server.reads.length).toBeGreaterThan(readsBefore);
  await expect(row('SHIP-170')).toHaveAttribute('data-section', 'backlog');
  expect(server.reorders).toHaveLength(0);
});

test('Planning_WhenCreateSprintClicked_ShouldPostTheServersDefaultsAndShowTheNewSection', async ({
  mount,
  page,
}) => {
  const view = planningView();
  const server = await stubBacklog(page, view);
  await mountPlanning(mount);
  await expect(page.getByTestId(ids.planningSectionTitle)).toHaveCount(4);
  const created = { id: 'sp27', name: 'Sprint 27', status: 'future' } as const;
  server.setBacklog({
    ...view,
    sections: [
      ...view.sections.slice(0, 3),
      { id: created.id, title: created.name, sprint: created, points: 0, capacity: null, rows: [] },
      view.sections[3],
    ],
  });

  await page.getByTestId(ids.planningCreateSprint).click();

  await expect
    .poll(() => server.sprintCreates)
    .toEqual([{ name: 'Sprint 27', startDate: '2026-11-03', endDate: '2026-11-16', goal: '' }]);
  await expect(page.getByTestId(ids.planningSectionTitle)).toHaveText([
    'Sprint 24',
    'Sprint 25',
    'Sprint 26',
    'Sprint 27',
    'Backlog',
  ]);
  await expect(page.getByTestId(componentTestIds.toast)).toContainText('Created Sprint 27');
});

test('Planning_WhenCreateSprintRefused_ShouldToastTheError', async ({ mount, page }) => {
  await stubBacklog(page, planningView(), {
    sprintCreateAnswer: { status: 400, body: { error: 'Name taken.', code: 'validation' } },
  });
  await mountPlanning(mount);

  await page.getByTestId(ids.planningCreateSprint).click();

  await expect(page.getByTestId(componentTestIds.toast)).toContainText('Name taken.');
  await expect(page.getByTestId(ids.planningSectionTitle)).toHaveCount(4);
});

test('Planning_WhenSprintsAreRead_ShouldShowEachSprintsDatesInItsHeader', async ({
  mount,
  page,
}) => {
  await stubBacklog(page, planningView(), { sprints: planningSprints() });
  await mountPlanning(mount);

  await expect(page.getByTestId(ids.planningSectionDates)).toHaveText([
    '20 Oct – 2 Nov',
    '3 Nov – 16 Nov',
    '17 Nov – 30 Nov',
  ]);
});
