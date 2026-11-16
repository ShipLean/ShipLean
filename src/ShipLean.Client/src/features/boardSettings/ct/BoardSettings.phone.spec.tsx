import { expect, test } from '@playwright/experimental-ct-react';
import type { Locator } from 'playwright';
import type { SwimlaneJson } from '@shiplean/core/api/persisted';
import { touchTestIds } from '../../../touch/testIds';
import { settingsDialogTestIds as ids } from '../../settingsDialog/testIds';
import { storiesSettings } from '../../../pages/screenshots/fixtures/boards';
import { boardSettingsTestIds as paneIds } from '../testIds';
import { BoardSettingsHarness, StoriesBoardSettingsHarness } from './BoardSettingsHarness';
import { BOARD_ID, boardSettingsDto, emptySettings, PROJECT_ID } from './boardSettingsFixtures';
import { boardHarnessIds } from './harnessIds';
import { stubBoard, styleRule } from './stubBoard';

type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];

interface Point {
  x: number;
  y: number;
}

// Hold at `from` for the K5 lift (450ms on the installed clock), then move to `to` in 20px steps and let go.
async function touchDrag(page: Page, from: Point, to: Point) {
  const cdp = await page.context().newCDPSession(page);
  const send = (type: 'touchStart' | 'touchMove' | 'touchEnd', point?: Point) =>
    cdp.send('Input.dispatchTouchEvent', { type, touchPoints: point ? [point] : [] });
  await send('touchStart', from);
  await page.clock.runFor(450);
  await expect(page.getByTestId(touchTestIds.dragGhost)).toBeVisible();
  const steps = Math.max(1, Math.ceil(Math.hypot(to.x - from.x, to.y - from.y) / 20));
  for (let i = 1; i <= steps; i++) {
    await send('touchMove', {
      x: Math.round(from.x + ((to.x - from.x) * i) / steps),
      y: Math.round(from.y + ((to.y - from.y) * i) / steps),
    });
  }
  await send('touchEnd');
}

/** The design sample (Storefront) on this harness's board and project ids. */
const storiesDto = (overrides: Parameters<typeof boardSettingsDto>[0] = {}) =>
  boardSettingsDto({ ...storiesSettings(), id: BOARD_ID, projectId: PROJECT_ID, ...overrides });

// The shared settings frame on a phone (SHIP-725), through the Board settings harness: section chips and the K5 SaveBar.

test('BoardSettings_WhenChangedOnPhone_ShouldShowSectionChipsAndSaveFromTheSaveBar', async ({
  mount,
  page,
}) => {
  const puts = await stubBoard(page, boardSettingsDto());
  await mount(<BoardSettingsHarness projectId={PROJECT_ID} boardId={BOARD_ID} apply="onSave" />);

  const annotations = page.getByTestId(`${ids.navItem}-annotations`);
  const swimlanes = page.getByTestId(`${ids.navItem}-swimlanes`);
  const first = await annotations.boundingBox();
  const second = await swimlanes.boundingBox();
  expect(first?.y, 'the chips are one row').toBe(second?.y);
  expect(first?.height).toBeGreaterThanOrEqual(32);
  await expect(annotations).toHaveAttribute('aria-current', 'page');
  await expect(page.getByTestId(touchTestIds.saveBar)).toHaveCount(0);

  await page.getByTestId(boardHarnessIds.toggleTasks).tap();
  const bar = page.getByTestId(touchTestIds.saveBar);
  await expect(bar.getByTestId(ids.footerDirty)).toHaveText('Unsaved: Annotations');
  await expect(page.getByTestId(`${ids.navDot}-annotations`)).toBeVisible();

  await bar.getByTestId(touchTestIds.saveBarSave).tap();
  await expect(page.getByTestId(boardHarnessIds.closed)).toBeAttached();
  expect(puts).toHaveLength(1);
  expect(puts[0]?.settings.annotations.tasks).toBe(false);
});

// SHIP-1849 (ND3b) gaps against the phone design settings-board-styles* (SHIP-1809 P4, P9).

test('StyleRules_WhenGripHeldAndDraggedOnPhone_ShouldReorderTheDraft', async ({ mount, page }) => {
  const blocked = styleRule('blocked', 'Blocked', {
    field: 'tags',
    op: 'contains',
    value: 'Blocked',
  });
  const spike = styleRule('spike', 'Spike', { field: 'tags', op: 'contains', value: 'Spike' });
  await stubBoard(
    page,
    boardSettingsDto({ settings: { ...emptySettings(), styleRules: [blocked, spike] } }),
    ['Blocked', 'Spike'],
  );
  await mount(
    <StoriesBoardSettingsHarness projectId={PROJECT_ID} boardId={BOARD_ID} section="styles" />,
  );
  const rules = page.getByTestId(paneIds.styleRow);
  await expect(rules).toHaveCount(2);
  await page.clock.install({ time: 0 });
  await page.clock.pauseAt(1000);
  const order = () =>
    page
      .getByTestId(paneIds.styleName)
      .evaluateAll((all) => all.map((e) => (e as HTMLInputElement).value));
  expect(await order()).toEqual(['Blocked', 'Spike']);
  const centre = async (locator: typeof rules): Promise<Point> => {
    const box = await locator.boundingBox();
    if (!box) throw new Error('no box');
    return { x: Math.round(box.x + box.width / 2), y: Math.round(box.y + box.height / 2) };
  };

  await touchDrag(
    page,
    await centre(rules.first().getByTestId(paneIds.styleGrip)),
    await centre(rules.last()),
  );

  await expect.poll(order).toEqual(['Spike', 'Blocked']);
  await expect(page.getByTestId(touchTestIds.saveBar).getByTestId(ids.footerDirty)).toHaveText(
    'Unsaved: Styles',
  );
});

test('StyleRules_WhenOnPhone_ShouldWrapTheHeadToTwoLinesInsideTheScreenWith32pxTargets', async ({
  mount,
  page,
}) => {
  await stubBoard(page, storiesDto(), ['Blocked', 'Spike']);
  await mount(
    <StoriesBoardSettingsHarness projectId={PROJECT_ID} boardId={BOARD_ID} section="styles" />,
  );
  const rule = page.getByTestId(paneIds.styleRow).first();
  const box = async (id: string) => {
    const found = await rule.getByTestId(id).boundingBox();
    if (!found) throw new Error(`no box for ${id}`);
    return found;
  };
  const grip = await box(paneIds.styleGrip);
  const name = await box(paneIds.styleName);
  const colour = await box(paneIds.styleColour);
  const remove = await box(paneIds.styleDelete);
  const bold = await box(paneIds.styleBold);
  const enabled = await box(paneIds.styleEnabled);
  const width = page.viewportSize()?.width ?? 0;

  // Line 1: ⠿ · name · colour · ✕, all on the screen.
  for (const [label, part] of [
    ['name', name],
    ['colour', colour],
    ['delete', remove],
  ] as const) {
    expect(Math.abs(part.y + part.height / 2 - (grip.y + grip.height / 2)), label).toBeLessThan(4);
  }
  expect(remove.x + remove.width, 'the ✕ stays on the screen').toBeLessThanOrEqual(width - 12);
  // Line 2: B I U · Enabled, under line 1.
  expect(bold.y, 'B I U on the second line').toBeGreaterThanOrEqual(grip.y + grip.height);
  expect(enabled.x + enabled.width).toBeLessThanOrEqual(width - 12);
  for (const [label, part] of [
    ['grip', grip],
    ['delete', remove],
    ['bold', bold],
    ['name', name],
  ] as const) {
    expect(part.height, `${label} is a 32px target`).toBeGreaterThanOrEqual(32);
  }
  await expect(page.getByTestId(paneIds.styleRow).first()).toContainText('Enabled');
});

test('Previews_WhenOnPhone_ShouldBeHiddenSoTheRowsTakeTheWidth', async ({ mount, page }) => {
  await stubBoard(page, storiesDto(), ['Blocked', 'Spike']);
  const component = await mount(
    <StoriesBoardSettingsHarness projectId={PROJECT_ID} boardId={BOARD_ID} section="styles" />,
  );
  const width = page.viewportSize()?.width ?? 0;
  await expect(page.getByTestId(paneIds.styleRow)).toHaveCount(3);
  await expect(page.getByTestId(paneIds.stylesPreview)).toBeHidden();
  const rule = await page.getByTestId(paneIds.styleRow).first().boundingBox();
  expect(rule?.width, 'a rule spans the screen less the gutters').toBeGreaterThan(width - 64);

  await component.update(
    <StoriesBoardSettingsHarness projectId={PROJECT_ID} boardId={BOARD_ID} section="tags" />,
  );
  await expect(page.getByTestId(paneIds.tagRow).first()).toBeVisible();
  await expect(page.getByTestId(paneIds.tagsPreview)).toBeHidden();
  const clear = await page.getByTestId(paneIds.tagClear).first().boundingBox();
  const swatches = await page.getByTestId(paneIds.tagSwatch).last().boundingBox();
  expect(
    (swatches?.x ?? 0) + (swatches?.width ?? 0),
    'swatches wrap inside the screen',
  ).toBeLessThanOrEqual(width);
  expect((clear?.x ?? 0) + (clear?.width ?? 0)).toBeLessThanOrEqual(width);

  await component.update(
    <StoriesBoardSettingsHarness projectId={PROJECT_ID} boardId={BOARD_ID} section="annotations" />,
  );
  await expect(page.getByTestId(paneIds.annotationTasks)).toBeVisible();
  await expect(page.getByTestId(paneIds.annotationsPreview)).toBeHidden();
});

test('Columns_WhenOnPhone_ShouldScrollTheTableSidewaysToReachTheStateMapping', async ({
  mount,
  page,
}) => {
  await stubBoard(page, storiesDto(), []);
  await mount(
    <StoriesBoardSettingsHarness projectId={PROJECT_ID} boardId={BOARD_ID} section="columns" />,
  );
  const state = page.getByTestId(paneIds.columnStateBug).first();
  await expect(page.getByTestId(paneIds.columnRow)).toHaveCount(5);
  await expect(state).not.toBeInViewport();
  const name = await page.getByTestId(paneIds.columnName).first().boundingBox();
  if (!name) throw new Error('no name box');

  await page.mouse.move(name.x + name.width / 2, name.y + name.height / 2);
  await page.mouse.wheel(2000, 0);

  await expect(state).toBeInViewport();
});

// SHIP-1985 (ND3b-7): column and lane grips get the K5 long-press drag and 32px targets at touch, as the rule rows do.

async function centreOf(locator: Locator): Promise<Point> {
  const box = await locator.boundingBox();
  if (!box) throw new Error('no box');
  return { x: Math.round(box.x + box.width / 2), y: Math.round(box.y + box.height / 2) };
}

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

const lanesDto = () => boardSettingsDto({ settings: { ...emptySettings(), swimlanes: lanes } });

test('Columns_WhenGripHeldAndDraggedOnPhone_ShouldReorderTheDraft', async ({ mount, page }) => {
  await stubBoard(page, storiesDto(), []);
  await mount(
    <StoriesBoardSettingsHarness projectId={PROJECT_ID} boardId={BOARD_ID} section="columns" />,
  );
  const rows = page.getByTestId(paneIds.columnRow);
  await expect(rows).toHaveCount(5);
  await page.clock.install({ time: 0 });
  await page.clock.pauseAt(1000);
  const order = () =>
    page
      .getByTestId(paneIds.columnName)
      .evaluateAll((all) => all.map((e) => (e as HTMLInputElement).value));
  expect(await order()).toEqual(['New', 'Ready', 'In progress', 'In review', 'Done']);

  await touchDrag(
    page,
    await centreOf(rows.nth(1).getByTestId(paneIds.columnGrip)),
    await centreOf(rows.nth(3).getByTestId(paneIds.columnName)),
  );

  await expect.poll(order).toEqual(['New', 'In progress', 'In review', 'Ready', 'Done']);
  await expect(page.getByTestId(touchTestIds.saveBar).getByTestId(ids.footerDirty)).toHaveText(
    'Unsaved: Columns',
  );
});

test('Lanes_WhenGripHeldAndDraggedOnPhone_ShouldReorderTheDraft', async ({ mount, page }) => {
  await stubBoard(page, lanesDto(), []);
  await mount(
    <StoriesBoardSettingsHarness projectId={PROJECT_ID} boardId={BOARD_ID} section="swimlanes" />,
  );
  const rows = page.getByTestId(paneIds.laneRow);
  await expect(rows).toHaveCount(3);
  await page.clock.install({ time: 0 });
  await page.clock.pauseAt(1000);
  const order = () =>
    page
      .getByTestId(paneIds.laneName)
      .evaluateAll((all) => all.map((e) => (e as HTMLInputElement).value));
  expect(await order()).toEqual(['Expedite', 'Tech debt', 'Default lane']);

  await touchDrag(
    page,
    await centreOf(rows.first().getByTestId(paneIds.laneGrip)),
    await centreOf(rows.nth(1).getByTestId(paneIds.laneName)),
  );

  await expect.poll(order).toEqual(['Tech debt', 'Expedite', 'Default lane']);
  await expect(page.getByTestId(touchTestIds.saveBar).getByTestId(ids.footerDirty)).toHaveText(
    'Unsaved: Swimlanes',
  );
});

test('ColumnAndLaneGrips_WhenOnPhone_ShouldBe32pxTargets', async ({ mount, page }) => {
  await stubBoard(
    page,
    storiesDto({ settings: { ...storiesDto().settings, swimlanes: lanes } }),
    [],
  );
  const component = await mount(
    <StoriesBoardSettingsHarness projectId={PROJECT_ID} boardId={BOARD_ID} section="columns" />,
  );
  await expect(page.getByTestId(paneIds.columnRow)).toHaveCount(5);
  const columnGrip = await page.getByTestId(paneIds.columnGrip).nth(1).boundingBox();
  expect(columnGrip?.width, 'column grip width').toBeGreaterThanOrEqual(32);
  expect(columnGrip?.height, 'column grip height').toBeGreaterThanOrEqual(32);

  await component.update(
    <StoriesBoardSettingsHarness projectId={PROJECT_ID} boardId={BOARD_ID} section="swimlanes" />,
  );
  await expect(page.getByTestId(paneIds.laneRow)).toHaveCount(3);
  const laneGrip = await page.getByTestId(paneIds.laneGrip).first().boundingBox();
  expect(laneGrip?.width, 'lane grip width').toBeGreaterThanOrEqual(32);
  expect(laneGrip?.height, 'lane grip height').toBeGreaterThanOrEqual(32);
});

// SHIP-1988: at touch the reorder hint names the grip as the design draws it (settings-board-styles).

test('ReorderHint_WhenOnPhone_ShouldShowTheGripGlyph', async ({ mount, page }) => {
  await stubBoard(page, lanesDto(), []);
  const component = await mount(
    <StoriesBoardSettingsHarness projectId={PROJECT_ID} boardId={BOARD_ID} section="styles" />,
  );
  await expect(page.getByTestId(paneIds.stylesPane)).toContainText('Drag ⠿ to reorder.');

  await component.update(
    <StoriesBoardSettingsHarness projectId={PROJECT_ID} boardId={BOARD_ID} section="swimlanes" />,
  );
  await expect(page.getByTestId(paneIds.swimlanesPane)).toContainText('Drag ⠿ to reorder.');
});
