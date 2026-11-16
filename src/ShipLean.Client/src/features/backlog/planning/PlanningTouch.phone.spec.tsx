import { expect, test } from '@playwright/experimental-ct-react';
import { componentTestIds } from '../../../components/testIds';
import { touchTestIds } from '../../../touch/testIds';
import { PROJECT, planningView } from '../ct/fixtures';
import { stubBacklog } from '../ct/stubApi';
import { backlogTestIds as ids } from '../testIds';
import { PlanningMode } from './PlanningMode';

// Planning at 900px and below (SHIP-716, L716d): sprint blocks, the section ⋯, ⋯ More, the pull line's ▲ ▼ and a row
// lifted over a sprint header, through real touch input. Runs in the CT phone project.

type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];

const route = { hooksConfig: { route: '/p/SHIP/backlogs/stories?mode=planning' } };
const saved = { collapsedSections: [], forecast: true };
const section = (page: Page, title: string) =>
  page
    .getByTestId(ids.planningSection)
    .filter({ has: page.getByTestId(ids.planningSectionTitle).getByText(title, { exact: true }) });
const actionRow = (id: string) =>
  `[data-testid="${touchTestIds.actionSheetRow}"][data-action-id="${id}"]`;

test('TouchSprintBlock_WhenOverCapacity_ShouldShowOver', async ({ mount, page }) => {
  await stubBacklog(page, planningView(), { saved });
  await mount(<PlanningMode project={PROJECT} />, route);

  const current = section(page, 'Sprint 24');
  await expect(current.getByTestId(componentTestIds.capacityBar)).toHaveAttribute(
    'data-tone',
    'over',
  );
  await expect(current.getByTestId(ids.planningCommitted)).toHaveText('26 / 23 pts');
  await expect(current.getByTestId(ids.planningOver)).toHaveText('+3 over');
  await expect(current.getByTestId(ids.planningFree)).toHaveCount(0);
  // The bar is the width of the block, not a 120px stub.
  const bar = await current.getByTestId(componentTestIds.capacityBar).boundingBox();
  expect(bar!.width).toBeGreaterThan(180);
  await expect(section(page, 'Sprint 25').getByTestId(ids.planningFree)).toHaveText('14 free');
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
    ),
  ).toBe(0);
});

test('PlanningSprintMore_OnAFutureSprint_ShouldOfferOpenSprint', async ({ mount, page }) => {
  await stubBacklog(page, planningView(), { saved });
  await mount(<PlanningMode project={PROJECT} />, route);
  // Nothing of the desktop header is left in the block.
  await expect(page.getByTestId(ids.planningSectionLink)).toHaveCount(0);
  await expect(page.getByTestId(ids.addHere)).toHaveCount(0);

  await section(page, 'Sprint 25').getByTestId(touchTestIds.moreButton).tap();
  await expect(page.locator(actionRow('add'))).toHaveText('+ Add');
  await expect(page.locator(actionRow('capacity'))).toHaveText('Capacity');
  await expect(page.locator(actionRow('open-sprint'))).toHaveText('Open sprint');
  await page.getByTestId(touchTestIds.sheetClose).tap();

  await section(page, 'Sprint 24').getByTestId(touchTestIds.moreButton).tap();
  await expect(page.locator(actionRow('open-sprint'))).toHaveText('Open sprint');
  await expect(page.locator(actionRow('capacity'))).toHaveCount(0);
  await page.locator(actionRow('add')).tap();
  await expect(page.getByTestId(ids.addRow)).toBeVisible();
});

test('PlanningAddInSection_AtTouch_ShouldUseASegmentedSwitchHelpLineAndClose', async ({
  mount,
  page,
}) => {
  await stubBacklog(page, planningView(), { saved });
  await mount(<PlanningMode project={PROJECT} />, route);
  await section(page, 'Sprint 24').getByTestId(touchTestIds.moreButton).tap();
  await page.locator(actionRow('add')).tap();

  const switchButtons = page.getByTestId(ids.addRow).getByTestId(ids.addType);
  await expect(switchButtons).toHaveText(['Story', 'Bug']);
  await expect(switchButtons.first()).toHaveAttribute('aria-pressed', 'true');
  await switchButtons.nth(1).tap();
  await expect(switchButtons.nth(1)).toHaveAttribute('aria-pressed', 'true');
  await expect(page.getByTestId(ids.addHelp)).toContainText('stays open for the next one');
  await expect(page.getByTestId(ids.addClose)).toHaveText('Close');
  await page.getByTestId(ids.addClose).tap();
  await expect(page.getByTestId(ids.addRow)).toHaveCount(0);
});

test('PullLine_WhenUpTapped_ShouldMoveOneItem', async ({ mount, page }) => {
  await stubBacklog(page, planningView(), { saved });
  await mount(<PlanningMode project={PROJECT} />, route);

  // Backlog: SHIP-170 (5), SHIP-171 (8), SHIP-172 (3). Sprint 25 holds 8 of 22: 5 + 8 fit.
  await expect(page.getByTestId(ids.pullSum)).toHaveText('8 + 13 = 21 / 22');
  await page.getByTestId(ids.pullUp).tap();
  await expect(page.getByTestId(ids.pullSum)).toHaveText('8 + 5 = 13 / 22');
  await expect(page.getByTestId(ids.pullContent)).toContainText('1 item');
  await expect(page.getByTestId(ids.pullReset)).toBeVisible();

  await page.getByTestId(ids.pullDown).tap();
  await page.getByTestId(ids.pullDown).tap();
  await expect(page.getByTestId(ids.pullSum)).toContainText('8 + 16 = 24 / 22');
  await expect(page.getByTestId(ids.pullDown)).toBeDisabled();
});

test('PlanningScreen_WhenTouchMoreTapped_ShouldOfferOverrideVelocityAndCreateSprint', async ({
  mount,
  page,
}) => {
  const server = await stubBacklog(page, planningView(), { saved });
  await mount(<PlanningMode project={PROJECT} />, route);

  await page.getByTestId(ids.toolbarRow).getByTestId(touchTestIds.moreButton).tap();
  await expect(page.locator(actionRow('override-velocity'))).toContainText('Override velocity');
  await expect(page.locator(actionRow('create-sprint'))).toHaveText('Create sprint');
  await expect(page.locator(actionRow('override-velocity'))).toContainText('21');
  await expect(page.getByTestId(ids.planningForecastToggle)).toHaveCount(0);

  await page.locator(actionRow('override-velocity')).tap();
  await page.getByTestId(ids.planningOverrideInput).fill('30');
  await page.getByTestId(ids.planningOverrideSave).tap();
  await expect.poll(() => server.overrides).toEqual([{ velocityOverride: 30 }]);

  await page.getByTestId(ids.toolbarRow).getByTestId(touchTestIds.moreButton).tap();
  await page.locator(actionRow('create-sprint')).tap();
  await expect.poll(() => server.sprintCreates.length).toBe(1);
});

test('PlanningTouch_WhenForecastOn_ShouldLabelEveryRowWithItsSprint', async ({ mount, page }) => {
  await stubBacklog(page, planningView(), { saved });
  await mount(<PlanningMode project={PROJECT} />, route);

  // SHIP-170 and SHIP-171 end Sprint 26; SHIP-172 is Sprint 27.
  await expect(page.getByTestId(ids.touchRowForecast)).toHaveText([
    '→ Sprint 26',
    '→ Sprint 26',
    '→ Sprint 27',
  ]);
});

test('PlanningTouch_WhenRowLiftedOverSprintHeader_ShouldLightItAndMoveOnDrop', async ({
  mount,
  page,
}) => {
  await page.clock.install({ time: 0 });
  const server = await stubBacklog(page, planningView(), { saved });
  await mount(<PlanningMode project={PROJECT} />, route);
  const cdp = await page.context().newCDPSession(page);
  const send = (type: 'touchStart' | 'touchMove' | 'touchEnd', x = -1, y = -1) =>
    cdp.send('Input.dispatchTouchEvent', {
      type,
      touchPoints: type === 'touchEnd' ? [] : [{ x, y }],
    });
  const handle = page
    .getByTestId(ids.row)
    .and(page.locator('[data-key="SHIP-170"]'))
    .getByTestId(ids.touchHandle);
  const from = (await handle.boundingBox())!;
  const header = (await section(page, 'Sprint 25').boundingBox())!;
  const x = Math.round(from.x + from.width / 2);
  const y = Math.round(from.y + from.height / 2);

  await send('touchStart', x, y);
  await page.clock.runFor(500);
  for (let i = 1; i <= 10; i++) {
    await send('touchMove', x, Math.round(y + ((header.y + 30 - y) * i) / 10));
  }

  const lit = section(page, 'Sprint 25');
  await expect(lit).toHaveAttribute('data-over', 'true');
  await expect(lit.getByTestId(ids.sectionPreview)).toHaveText('8 + 5 = 13 / 22 pts');
  await expect(lit.getByTestId(ids.planningDropMessage)).toHaveText(
    'Let go to move SHIP-170 into Sprint 25',
  );
  await expect(section(page, 'Sprint 24')).toHaveAttribute('data-over', 'false');

  await send('touchEnd');
  await expect(lit).toHaveAttribute('data-over', 'false');
  await expect.poll(() => server.patches.length).toBe(1);
  expect(server.patches[0].body).toMatchObject({ changes: { sprint: { value: 'sp25' } } });
  expect(server.reorders).toHaveLength(0);
});

test('PlanningMore_Forecast_ShouldToggleWithoutClosing', async ({ mount, page }) => {
  await stubBacklog(page, planningView(), { saved });
  await mount(<PlanningMode project={PROJECT} />, route);
  await expect(page.getByTestId(ids.planningVelocity)).toBeVisible();

  await page.getByTestId(ids.toolbarRow).getByTestId(touchTestIds.moreButton).tap();
  const forecast = page.locator(
    `[data-testid="${touchTestIds.actionSheetToggle}"][data-action-id="forecast"]`,
  );
  await expect(forecast).toHaveAttribute('aria-checked', 'true');
  await forecast.tap();
  // The switch flips and the sheet stays open; the forecast line leaves the Backlog header behind it.
  await expect(forecast).toHaveAttribute('aria-checked', 'false');
  await expect(page.getByTestId(touchTestIds.sheetClose)).toBeVisible();
  await expect(page.locator(actionRow('override-velocity'))).toHaveCount(0);
});

test('PlanningMore_AddMany_ShouldBeASheetAction', async ({ mount, page }) => {
  await stubBacklog(page, planningView(), { saved });
  await mount(<PlanningMode project={PROJECT} />, route);

  await page.getByTestId(ids.toolbarRow).getByTestId(touchTestIds.moreButton).tap();
  await expect(page.locator(actionRow('add-many'))).toContainText('Add many');
  await expect(page.getByTestId(ids.addMany)).toHaveCount(0);
});

test('PlanningTouchRow_InItsOwnSprintSection_ShouldHideTheSprintChipAndStackPointsOverTheAvatar', async ({
  mount,
  page,
}) => {
  await stubBacklog(page, planningView(), { saved });
  await mount(<PlanningMode project={PROJECT} />, route);

  // SHIP-158 is the Sprint 25 row.
  const row = page.locator(`[data-testid="${ids.row}"][data-key="SHIP-158"]`);
  await expect(row).toBeVisible();
  await expect(row.getByTestId(ids.sprintChip)).toHaveCount(0);
  const points = await row.getByTestId(ids.points).boundingBox();
  const avatar = await row.getByTestId(ids.assignee).boundingBox();
  expect(points).not.toBeNull();
  expect(avatar).not.toBeNull();
  expect(points!.y + points!.height).toBeLessThanOrEqual(avatar!.y + 1);
});
