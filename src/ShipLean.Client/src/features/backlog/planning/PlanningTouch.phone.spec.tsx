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

test('TouchSprintBlock_WhenMoreTapped_ShouldOfferAddCapacityOpenSprint', async ({
  mount,
  page,
}) => {
  await stubBacklog(page, planningView(), { saved });
  await mount(<PlanningMode project={PROJECT} />, route);
  // Nothing of the desktop header is left in the block.
  await expect(page.getByTestId(ids.planningSectionLink)).toHaveCount(0);
  await expect(page.getByTestId(ids.addHere)).toHaveCount(0);

  await section(page, 'Sprint 25').getByTestId(touchTestIds.moreButton).tap();
  await expect(page.locator(actionRow('add'))).toHaveText('+ Add');
  await expect(page.locator(actionRow('capacity'))).toHaveText('Capacity');
  await expect(page.locator(actionRow('open-sprint'))).toHaveCount(0);
  await page.getByTestId(touchTestIds.sheetClose).tap();

  await section(page, 'Sprint 24').getByTestId(touchTestIds.moreButton).tap();
  await expect(page.locator(actionRow('open-sprint'))).toHaveText('Open sprint');
  await expect(page.locator(actionRow('capacity'))).toHaveCount(0);
  await page.locator(actionRow('add')).tap();
  await expect(page.getByTestId(ids.addRow)).toBeVisible();
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
  await expect(page.locator(actionRow('override-velocity'))).toHaveText('Override velocity');
  await expect(page.locator(actionRow('create-sprint'))).toHaveText('Create sprint');
  await expect(page.getByTestId(ids.planningForecastToggle)).toBeVisible();

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
  const send = (type: 'touchStart' | 'touchMove' | 'touchEnd', x?: number, y?: number) =>
    cdp.send('Input.dispatchTouchEvent', {
      type,
      touchPoints: x === undefined ? [] : [{ x, y }],
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
