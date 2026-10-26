import { expect, test } from '@playwright/experimental-ct-react';
import type { BoardLevel, BoardSettingsDto, BoardSettingsPut } from '../../../../api/boards';
import type { SwimlaneJson } from '../../../../api/persisted';
import { criteriaTestIds } from '../../../criteria/testIds';
import { settingsDialogTestIds as dialogIds } from '../../../settingsDialog/testIds';
import { BoardSettingsDialog } from '../../BoardSettingsDialog';
import {
  BOARD_ID,
  boardSettingsDto,
  emptySettings,
  PROJECT_ID,
} from '../../ct/boardSettingsFixtures';
import { boardSettingsTestIds as ids } from '../../testIds';

const lanes: SwimlaneJson[] = [
  {
    id: 'lane-expedite',
    name: 'Expedite',
    colour: 'danger',
    kind: 'criteria',
    criteria: { match: 'all', clauses: [{ field: 'priority', op: '=', value: 1 }] },
  },
  { id: 'lane-debt', name: 'Tech debt', colour: null, kind: 'manual', criteria: null },
  { id: 'lane-default', name: 'Default lane', colour: null, kind: 'default', criteria: null },
];

interface StubbedRoute {
  request(): { method(): string; postDataJSON(): unknown };
  fulfill(response: { json: unknown; status?: number }): Promise<void>;
}
interface Routable {
  route(url: string, handler: (route: StubbedRoute) => Promise<void>): Promise<unknown>;
}

async function stubBoard(
  page: Routable,
  level: BoardLevel = 'stories',
): Promise<BoardSettingsPut[]> {
  const dto: BoardSettingsDto = boardSettingsDto({
    level,
    settings: { ...emptySettings(), swimlanes: lanes },
  });
  const puts: BoardSettingsPut[] = [];
  await page.route(`**/api/projects/${PROJECT_ID}/boards`, (route) =>
    route.fulfill({ json: [{ id: BOARD_ID, level, name: level }] }),
  );
  await page.route(`**/api/projects/${PROJECT_ID}/members`, (route) => route.fulfill({ json: [] }));
  await page.route(`**/api/projects/${PROJECT_ID}/sprints`, (route) => route.fulfill({ json: [] }));
  await page.route(`**/api/boards/${BOARD_ID}/settings`, async (route) => {
    if (route.request().method() === 'GET') return route.fulfill({ json: dto });
    const body = route.request().postDataJSON() as BoardSettingsPut;
    puts.push(body);
    return route.fulfill({
      json: { board: { ...dto, settings: body.settings }, mergedSections: [] },
    });
  });
  return puts;
}

const mountLanes = (
  mount: (component: React.JSX.Element) => Promise<unknown>,
  level: BoardLevel = 'stories',
) =>
  mount(
    <BoardSettingsDialog
      projectId={PROJECT_ID}
      level={level}
      section="swimlanes"
      onSectionChange={() => undefined}
      onClose={() => undefined}
    />,
  );

test('SwimlanesPane_WhenAltArrowPressedOnGrip_ShouldReorderAndPutTheOrder', async ({
  mount,
  page,
}) => {
  const puts = await stubBoard(page);
  await mountLanes(mount);

  await page.getByTestId(ids.laneGrip).first().focus();
  await page.keyboard.press('Alt+ArrowDown');
  await page.getByTestId(dialogIds.save).click();

  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0].settings.swimlanes.map((l) => l.id)).toEqual([
    'lane-debt',
    'lane-expedite',
    'lane-default',
  ]);
});

test('SwimlanesPane_WhenLaneRenamedAndRecoloured_ShouldPutIt', async ({ mount, page }) => {
  const puts = await stubBoard(page);
  await mountLanes(mount);

  await page.getByTestId(ids.laneName).nth(1).fill('Debt');
  await page.getByTestId(ids.laneColour).nth(1).selectOption('purple');
  await page.getByTestId(dialogIds.save).click();

  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0].settings.swimlanes[1]).toMatchObject({ name: 'Debt', colour: 'purple' });
});

test('SwimlanesPane_WhenClauseAdded_ShouldPutTheCriteria', async ({ mount, page }) => {
  const puts = await stubBoard(page);
  await mountLanes(mount);

  await page.getByTestId(criteriaTestIds.add).click();
  await page.getByTestId(dialogIds.save).click();

  await expect.poll(() => puts.length).toBe(1);
  const criteria = puts[0].settings.swimlanes[0].criteria;
  expect(criteria?.clauses).toHaveLength(2);
  expect(criteria?.clauses[0]).toEqual({ field: 'priority', op: '=', value: 1 });
});

test('SwimlanesPane_WhenCriteriaCannotTakeDrops_ShouldSayCardsCannotBeDroppedIn', async ({
  mount,
  page,
}) => {
  await stubBoard(page);
  await mountLanes(mount);
  await expect(page.getByTestId(ids.laneNoDrops)).toHaveCount(0);

  // A new clause starts on a type, which the board cannot write onto a dropped card.
  await page.getByTestId(criteriaTestIds.add).click();

  await expect(page.getByTestId(ids.laneNoDrops)).toHaveCount(1);
  await expect(page.getByTestId(ids.laneNoDrops)).toContainText(
    "Cards can't be dropped into this lane",
  );
});

test('SwimlanesPane_WhenCriteriaLaneHasNoClauses_ShouldSayCardsCannotBeDroppedInOrDraggedOut', async ({
  mount,
  page,
}) => {
  await stubBoard(page);
  await mountLanes(mount);
  await expect(page.getByTestId(ids.laneNoDrops)).toHaveCount(0);

  await page.getByTestId(criteriaTestIds.remove).first().click();

  await expect(page.getByTestId(ids.laneNoDrops)).toHaveCount(1);
  await expect(page.getByTestId(ids.laneNoDrops)).toContainText('has no criteria');
  await expect(page.getByTestId(ids.laneNoDrops)).toContainText("can't be dragged out of it");
});

test('SwimlanesPane_WhenLaneAddedAsCriteria_ShouldPutOneClause', async ({ mount, page }) => {
  const puts = await stubBoard(page);
  await mountLanes(mount);

  await page.getByTestId(ids.lanesAdd).click();
  await expect(page.getByTestId(ids.laneRow)).toHaveCount(4);
  await page.getByTestId(ids.laneName).nth(3).fill('Urgent');
  await page.getByTestId(ids.laneModeCriteria).nth(2).click();
  await page.getByTestId(dialogIds.save).click();

  await expect.poll(() => puts.length).toBe(1);
  const added = puts[0].settings.swimlanes[3];
  expect(added).toMatchObject({ name: 'Urgent', kind: 'criteria' });
  expect(added.criteria?.clauses).toHaveLength(1);
});

test('SwimlanesPane_WhenLaneDeleted_ShouldDropItFromThePut', async ({ mount, page }) => {
  const puts = await stubBoard(page);
  await mountLanes(mount);

  await page.getByTestId(ids.laneDelete).nth(1).click();
  await expect(page.getByTestId(ids.laneRow)).toHaveCount(2);
  await page.getByTestId(dialogIds.save).click();

  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0].settings.swimlanes.map((l) => l.id)).toEqual(['lane-expedite', 'lane-default']);
});

test('SwimlanesPane_WhenDefaultLaneDeleted_ShouldRefuseAndKeepIt', async ({ mount, page }) => {
  await stubBoard(page);
  await mountLanes(mount);

  await page.getByTestId(ids.laneDelete).nth(2).click();

  await expect(page.getByTestId(ids.laneRefusal)).toContainText(
    "The default lane can't be deleted",
  );
  await expect(page.getByTestId(ids.laneRow)).toHaveCount(3);
});

test('SwimlanesPane_WhenFeaturesBoard_ShouldSayThereAreNoStoredLanes', async ({ mount, page }) => {
  await stubBoard(page, 'features');
  await mountLanes(mount, 'features');

  await expect(page.getByTestId(ids.lanesNotStored)).toContainText('no stored lanes');
  await expect(page.getByTestId(ids.laneRow)).toHaveCount(0);
});
