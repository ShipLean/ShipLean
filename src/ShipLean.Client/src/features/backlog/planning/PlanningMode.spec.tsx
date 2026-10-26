import { expect, test } from '@playwright/experimental-ct-react';
import { componentTestIds } from '../../../components/testIds';
import { PROJECT, planningView } from '../ct/fixtures';
import { stubBacklog } from '../ct/stubApi';
import { backlogTestIds as ids } from '../testIds';
import { PlanningMode } from './PlanningMode';

const route = { hooksConfig: { route: '/p/SHIP/backlogs/stories?mode=planning' } };

test('Planning_WhenLoaded_ShouldStackEverySectionExpandedAboveTheBacklog', async ({
  mount,
  page,
}) => {
  const server = await stubBacklog(page, planningView());
  await mount(<PlanningMode project={PROJECT} />, route);

  await expect(page.getByTestId(ids.planningSectionTitle)).toHaveText([
    'Sprint 24',
    'Sprint 25',
    'Sprint 26',
    'Backlog',
  ]);
  expect(server.reads[0]).toContain('mode=planning');
  await expect(page.getByTestId(ids.row)).toHaveCount(5);
  await expect(page.getByTestId(ids.planningSectionCount).first()).toHaveText('1 items');
  await expect(page.getByTestId(ids.planningSectionCount).last()).toHaveText('3 items · 16 pts');
  await expect(page.getByTestId(ids.planningEmptySection)).toContainText('Sprint 26');
  await expect(
    page.getByTestId(ids.planningSection).first().getByTestId(componentTestIds.chip),
  ).toHaveText('Current');
});

test('Planning_WhenOverCapacity_ShouldWarnNotBlock', async ({ mount, page }) => {
  await stubBacklog(page, planningView());
  await mount(<PlanningMode project={PROJECT} />, route);

  const current = page.getByTestId(ids.planningSection).nth(0);
  await expect(current.getByTestId(componentTestIds.capacityBar)).toHaveAttribute(
    'data-tone',
    'over',
  );
  await expect(current.getByTestId(ids.planningCommitted)).toHaveText('26 / 23 pts');
  await expect(current.getByTestId(ids.planningOver)).toHaveText('+3 over');
  await expect(current.getByTestId(ids.planningFree)).toHaveCount(0);
  // Nothing is blocked: the new item row still opens.
  await page.getByTestId(ids.newItem).click();
  await expect(page.getByTestId(ids.addRow)).toBeVisible();
});

test('Planning_WhenUnderCapacity_ShouldShowThePointsFree', async ({ mount, page }) => {
  await stubBacklog(page, planningView());
  await mount(<PlanningMode project={PROJECT} />, route);

  const future = page.getByTestId(ids.planningSection).nth(1);
  await expect(future.getByTestId(componentTestIds.capacityBar)).toHaveAttribute(
    'data-tone',
    'normal',
  );
  await expect(future.getByTestId(ids.planningFree)).toHaveText('14 free');
  await expect(future.getByTestId(ids.planningOver)).toHaveCount(0);
});

test('Planning_WhenNoCapacity_ShouldDrawTheMutedBarAgainstVelocityBesideTheChip', async ({
  mount,
  page,
}) => {
  await stubBacklog(page, planningView());
  await mount(<PlanningMode project={PROJECT} />, route);

  const later = page.getByTestId(ids.planningSection).nth(2);
  await expect(later.getByTestId(ids.planningVelocityChip)).toHaveText('velocity 21');
  await expect(later.getByTestId(componentTestIds.capacityBar)).toHaveAttribute(
    'data-tone',
    'velocity',
  );
  await expect(later.getByTestId(ids.planningCommitted)).toHaveText('0 / 21 pts');
  await expect(later.getByTestId(ids.planningFree)).toHaveText('21 free');
});

test('Planning_WhenNoCapacityAndMoreThanVelocity_ShouldShowThePointsOverVelocity', async ({
  mount,
  page,
}) => {
  const view = planningView();
  const sections = view.sections.map((s) => (s.id === 'sp26' ? { ...s, points: 25 } : s));
  await stubBacklog(page, { ...view, sections });
  await mount(<PlanningMode project={PROJECT} />, route);

  const later = page.getByTestId(ids.planningSection).nth(2);
  await expect(later.getByTestId(componentTestIds.capacityBar)).toHaveAttribute(
    'data-tone',
    'over',
  );
  await expect(later.getByTestId(ids.planningOver)).toHaveText('+4 over');
});

test('Planning_WhenCommittedEqualsCapacity_ShouldShowZeroFreeNotOver', async ({ mount, page }) => {
  const view = planningView();
  const sections = view.sections.map((s) => (s.id === 'sp25' ? { ...s, capacity: 8 } : s));
  await stubBacklog(page, { ...view, sections });
  await mount(<PlanningMode project={PROJECT} />, route);

  const future = page.getByTestId(ids.planningSection).nth(1);
  await expect(future.getByTestId(ids.planningFree)).toHaveText('0 free');
  await expect(future.getByTestId(ids.planningOver)).toHaveCount(0);
  await expect(future.getByTestId(componentTestIds.capacityBar)).toHaveAttribute(
    'data-tone',
    'normal',
  );
});

test('Planning_WhenForecastOn_ShouldDrawCutLinesUnderTheLastItemOfEachSprint', async ({
  mount,
  page,
}) => {
  await stubBacklog(page, planningView());
  await mount(<PlanningMode project={PROJECT} />, route);

  const lines = page.getByTestId(ids.planningForecastLine);
  await expect(lines).toHaveText(['→ Sprint 26', '→ Sprint 27']);
  const marked = page
    .getByTestId(ids.row)
    .filter({ has: page.getByTestId(ids.planningForecastLine) });
  await expect(marked).toHaveCount(2);
  await expect(marked.nth(0)).toHaveAttribute('data-key', 'SHIP-171');
  await expect(marked.nth(1)).toHaveAttribute('data-key', 'SHIP-172');
  await expect(page.getByTestId(ids.planningVelocity)).toContainText(
    'Forecast at velocity 21 (average of last 3 sprints: 20 · 22 · 21)',
  );
});

test('Planning_WhenForecastToggledOff_ShouldHideLinesAndSaveTheChoice', async ({ mount, page }) => {
  const server = await stubBacklog(page, planningView());
  await mount(<PlanningMode project={PROJECT} />, route);
  await expect(page.getByTestId(ids.planningForecastLine)).toHaveCount(2);

  // click, not uncheck(): the box follows the saved preference, which lands a tick after the click.
  await page.getByTestId(ids.planningForecastToggle).click();
  await expect(page.getByTestId(ids.planningForecastToggle)).not.toBeChecked();

  await expect(page.getByTestId(ids.planningForecastLine)).toHaveCount(0);
  await expect(page.getByTestId(ids.planningForecastLabel)).toHaveCount(0);
  await expect(page.getByTestId(ids.planningOverrideButton)).toHaveCount(0);
  await expect.poll(() => server.puts).toEqual([{ forecast: false }]);
});

test('Planning_WhenForecastSavedOff_ShouldOpenWithoutLines', async ({ mount, page }) => {
  await stubBacklog(page, planningView(), { saved: { forecast: false } });
  await mount(<PlanningMode project={PROJECT} />, route);

  await expect(page.getByTestId(ids.row)).toHaveCount(5);
  await expect(page.getByTestId(ids.planningForecastLine)).toHaveCount(0);
  await expect(page.getByTestId(ids.planningForecastToggle)).not.toBeChecked();
});

test('Planning_WhenOverrideSet_ShouldSendItAndMoveTheForecastLines', async ({ mount, page }) => {
  const view = planningView();
  const server = await stubBacklog(page, view);
  await mount(<PlanningMode project={PROJECT} />, route);
  await page.getByTestId(ids.planningOverrideButton).click();
  await expect(page.getByTestId(ids.planningOverrideInput)).toHaveValue('21');

  // The server answers the next read with the lines where a velocity of 30 puts them.
  const backlog = view.sections[3].rows;
  server.setBacklog(
    planningView({
      velocity: { ...view.velocity!, override: 30 },
      forecast: {
        sprints: [{ ...view.forecast!.sprints[0], lastItemId: backlog[2].id }],
      },
    }),
  );
  await page.getByTestId(ids.planningOverrideInput).fill('30');
  await page.getByTestId(ids.planningOverrideSave).click();

  await expect.poll(() => server.overrides).toEqual([{ velocityOverride: 30 }]);
  const lines = page.getByTestId(ids.planningForecastLine);
  await expect(lines).toHaveCount(1);
  await expect(
    page.getByTestId(ids.row).filter({ has: page.getByTestId(ids.planningForecastLine) }),
  ).toHaveAttribute('data-key', 'SHIP-172');
  await expect(page.getByTestId(ids.planningVelocity)).toHaveAttribute('data-overridden', 'true');
  await expect(page.getByTestId(ids.planningVelocity)).toContainText('velocity 30 (override)');
});

test('Planning_WhenOverrideInUse_ShouldOfferTheAverageAndSendNull', async ({ mount, page }) => {
  const view = planningView();
  const server = await stubBacklog(page, {
    ...view,
    velocity: { ...view.velocity!, override: 30 },
  });
  await mount(<PlanningMode project={PROJECT} />, route);
  await page.getByTestId(ids.planningOverrideButton).click();
  await page.getByTestId(ids.planningOverrideClear).click();

  await expect.poll(() => server.overrides).toEqual([{ velocityOverride: null }]);
});

test('Planning_WhenOverrideEmpty_ShouldNotSave', async ({ mount, page }) => {
  await stubBacklog(page, planningView());
  await mount(<PlanningMode project={PROJECT} />, route);
  await page.getByTestId(ids.planningOverrideButton).click();
  await page.getByTestId(ids.planningOverrideInput).fill('');
  await expect(page.getByTestId(ids.planningOverrideSave)).toBeDisabled();
});

test('Planning_WhenOverrideRefused_ShouldShowTheError', async ({ mount, page }) => {
  await stubBacklog(page, planningView());
  await page.route('**/api/projects/p1/forecast', (r) =>
    r.fulfill({
      status: 400,
      contentType: 'application/json',
      body: JSON.stringify({ error: 'Velocity must be positive.', code: 'validation' }),
    }),
  );
  await mount(<PlanningMode project={PROJECT} />, route);
  await page.getByTestId(ids.planningOverrideButton).click();
  await page.getByTestId(ids.planningOverrideSave).click();
  await expect(page.getByTestId(ids.planningOverrideError)).toHaveText(
    'Velocity must be positive.',
  );
});

test('Planning_ShouldLinkEachSectionToItsSprint', async ({ mount, page }) => {
  await stubBacklog(page, planningView());
  await mount(<PlanningMode project={PROJECT} />, route);

  const links = page.getByTestId(ids.planningSectionLink);
  await expect(links.nth(0)).toHaveText('Open sprint');
  await expect(links.nth(0)).toHaveAttribute('href', '/p/SHIP/sprints/sp24/backlog');
  await expect(links.nth(1)).toHaveText('Capacity');
  await expect(links.nth(1)).toHaveAttribute('href', '/p/SHIP/sprints/sp25/capacity');
});

test('Planning_ToolbarShouldHaveNeitherCompletedNorColumns', async ({ mount, page }) => {
  await stubBacklog(page, planningView());
  await mount(<PlanningMode project={PROJECT} />, route);

  await expect(page.getByTestId(ids.modeSwitch)).toBeVisible();
  await expect(page.getByTestId(ids.newItem)).toBeVisible();
  await expect(page.getByTestId(ids.planningForecastToggle)).toBeVisible();
  await expect(page.getByTestId(ids.completedToggle)).toHaveCount(0);
  await expect(page.getByTestId(ids.columnsButton)).toHaveCount(0);
});

test('Planning_WhenNewItemAdded_ShouldOpenTheRowInTheBacklogSection', async ({ mount, page }) => {
  const server = await stubBacklog(page, planningView());
  await mount(<PlanningMode project={PROJECT} />, route);
  await page.getByTestId(ids.newItem).click();
  await page.getByTestId(ids.addTitle).fill('Fresh idea');
  await page.getByTestId(ids.addTitle).press('Enter');
  await expect.poll(() => server.creates.length).toBe(1);
  expect(server.creates[0]).toMatchObject({ title: 'Fresh idea', sprintId: null });
});

test('Planning_WhenReadFails_ShouldSayItCouldNotLoad', async ({ mount, page }) => {
  await stubBacklog(page, planningView(), { answer: () => ({ status: 500, body: {} }) });
  await mount(<PlanningMode project={PROJECT} />, route);
  await expect(page.getByTestId(ids.loadFailed)).toBeVisible();
});
