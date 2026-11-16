import { expect, test } from '@playwright/experimental-ct-react';
import type { Locator, Page } from 'playwright';
import type { SwimlaneJson } from '@shiplean/core/api/persisted';
import { touchTestIds } from '../../../touch/testIds';
import { settingsDialogTestIds as ids } from '../../settingsDialog/testIds';
import { storiesSettings } from '../../../pages/screenshots/fixtures/boards';
import { boardSettingsTestIds as paneIds } from '../testIds';
import { BoardSettingsHarness, StoriesBoardSettingsHarness } from './BoardSettingsHarness';
import { BOARD_ID, boardSettingsDto, emptySettings, PROJECT_ID } from './boardSettingsFixtures';
import { boardHarnessIds } from './harnessIds';
import { stubBoard, styleRule } from './stubBoard';

interface Point {
  x: number;
  y: number;
}

// Hold at `from` for the K5 lift (450ms on the installed clock), then move to `to` in 20px steps and let go.
// With `hold`, the finger stays down at `to` and the caller sends the end (touchEnd or touchCancel).
async function touchDrag(page: Page, from: Point, to: Point, hold = false) {
  const cdp = await page.context().newCDPSession(page);
  const send = (type: 'touchStart' | 'touchMove' | 'touchEnd' | 'touchCancel', point?: Point) =>
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
  if (!hold) await send('touchEnd');
  return send;
}

async function centreOf(locator: Locator): Promise<Point> {
  const box = await locator.boundingBox();
  if (!box) throw new Error('no box');
  return { x: Math.round(box.x + box.width / 2), y: Math.round(box.y + box.height / 2) };
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

  await touchDrag(
    page,
    await centreOf(rules.first().getByTestId(paneIds.styleGrip)),
    await centreOf(rules.last()),
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
  // The table scrolled inside its panel: the page itself never scrolls sideways.
  const scroll = await page.evaluate(() => ({
    scrollX: window.scrollX,
    scrollWidth: document.scrollingElement?.scrollWidth ?? 0,
    width: window.innerWidth,
  }));
  expect(scroll.scrollX, 'the page stays put').toBe(0);
  expect(scroll.scrollWidth, 'the page is no wider than the screen').toBeLessThanOrEqual(
    scroll.width,
  );
});

// SHIP-1985 (ND3b-7): column and lane grips get the K5 long-press drag and 32px targets at touch, as the rule rows do.

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

test('Lanes_WhenOnPhone_ShouldWrapTheHeadInsideTheScreen', async ({ mount, page }) => {
  await stubBoard(page, lanesDto(), []);
  await mount(
    <StoriesBoardSettingsHarness projectId={PROJECT_ID} boardId={BOARD_ID} section="swimlanes" />,
  );
  await expect(page.getByTestId(paneIds.laneRow)).toHaveCount(3);
  const width = page.viewportSize()?.width ?? 0;
  const expedite = page.getByTestId(paneIds.laneRow).first();
  for (const id of [paneIds.laneColour, paneIds.laneModeManual, paneIds.laneDelete]) {
    const box = await expedite.getByTestId(id).boundingBox();
    expect((box?.x ?? 0) + (box?.width ?? 0), `${id} stays on the screen`).toBeLessThanOrEqual(
      width - 12,
    );
  }
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

// SHIP-1989: while a row is lifted, the row under the finger draws the drop line on the side the dragged row lands
// (design settings-board-styles-drag); a touch the browser cancels clears it and moves nothing.

const dropLineCases = [
  {
    pane: 'StyleRules',
    section: 'styles',
    dto: () => storiesDto(),
    tags: ['Blocked', 'Spike'],
    row: paneIds.styleRow,
    name: paneIds.styleName,
    grip: paneIds.styleGrip,
    from: 0,
    to: 1,
    side: 'below',
    order: ['Blocked', 'Spike', 'Expedite'],
  },
  {
    pane: 'Columns',
    section: 'columns',
    dto: () => storiesDto(),
    tags: [],
    row: paneIds.columnRow,
    name: paneIds.columnName,
    grip: paneIds.columnGrip,
    from: 3,
    to: 1,
    side: 'above',
    order: ['New', 'Ready', 'In progress', 'In review', 'Done'],
  },
  {
    pane: 'Lanes',
    section: 'swimlanes',
    dto: lanesDto,
    tags: [],
    row: paneIds.laneRow,
    name: paneIds.laneName,
    grip: paneIds.laneGrip,
    from: 0,
    to: 1,
    side: 'below',
    order: ['Expedite', 'Tech debt', 'Default lane'],
  },
] as const;

for (const c of dropLineCases) {
  test(`${c.pane}_WhenGripDraggedOverARowOnPhone_ShouldDrawTheDropLineUntilCancelled`, async ({
    mount,
    page,
  }) => {
    await stubBoard(page, c.dto(), [...c.tags]);
    await mount(
      <StoriesBoardSettingsHarness projectId={PROJECT_ID} boardId={BOARD_ID} section={c.section} />,
    );
    const rows = page.getByTestId(c.row);
    await expect(rows).toHaveCount(c.order.length);
    await page.clock.install({ time: 0 });
    await page.clock.pauseAt(1000);
    const order = () =>
      page.getByTestId(c.name).evaluateAll((all) => all.map((e) => (e as HTMLInputElement).value));
    expect(await order()).toEqual(c.order);
    // The line is an inset signal shadow: on the row itself, or on a table row's cells.
    const shadow = () =>
      rows.nth(c.to).evaluate((e) => {
        const drawn = e.tagName === 'TR' ? e.firstElementChild : e;
        return drawn ? getComputedStyle(drawn).boxShadow : 'none';
      });
    expect(await shadow()).toBe('none');

    const send = await touchDrag(
      page,
      await centreOf(rows.nth(c.from).getByTestId(c.grip)),
      await centreOf(rows.nth(c.to).getByTestId(c.name)),
      true,
    );

    await expect.poll(shadow).toContain(c.side === 'below' ? '0px -2px 0px' : '0px 2px 0px');
    expect(await shadow()).toContain('inset');
    await send('touchCancel');
    await expect(page.getByTestId(touchTestIds.dragGhost)).toHaveCount(0);
    await expect.poll(shadow).toBe('none');
    expect(await order()).toEqual(c.order);
  });
}

// SHIP-1985 D1: the first and last columns are fixed, so holding their grip lifts nothing.
test('Columns_WhenFixedColumnGripHeldOnPhone_ShouldNotLift', async ({ mount, page }) => {
  await stubBoard(page, storiesDto(), []);
  await mount(
    <StoriesBoardSettingsHarness projectId={PROJECT_ID} boardId={BOARD_ID} section="columns" />,
  );
  const rows = page.getByTestId(paneIds.columnRow);
  await expect(rows).toHaveCount(5);
  await page.clock.install({ time: 0 });
  await page.clock.pauseAt(1000);
  const cdp = await page.context().newCDPSession(page);

  for (const row of [rows.first(), rows.last()]) {
    const grip = await centreOf(row.getByTestId(paneIds.columnGrip));
    await cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [grip] });
    await page.clock.runFor(450);
    await expect(page.getByTestId(touchTestIds.dragGhost)).toHaveCount(0);
    await cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
  }
  // The control: a movable column's grip does lift on the same hold.
  const movable = await centreOf(rows.nth(1).getByTestId(paneIds.columnGrip));
  await cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [movable] });
  await page.clock.runFor(450);
  await expect(page.getByTestId(touchTestIds.dragGhost)).toBeVisible();
  await cdp.send('Input.dispatchTouchEvent', { type: 'touchCancel', touchPoints: [] });
});

// SHIP-1990: each Annotations roll-up row (its checkbox and label) is a 32px target at touch.
test('Annotations_WhenOnPhone_ShouldHave32pxCheckboxRows', async ({ mount, page }) => {
  await stubBoard(page, storiesDto(), []);
  await mount(
    <StoriesBoardSettingsHarness projectId={PROJECT_ID} boardId={BOARD_ID} section="annotations" />,
  );
  await expect(page.getByTestId(paneIds.annotationTasks)).toBeVisible();
  // The Stories board lists three roll-ups (Features is listed on the Features and Epics boards only).
  for (const id of [paneIds.annotationTasks, paneIds.annotationStories, paneIds.annotationBugs]) {
    const height = await page
      .getByTestId(id)
      .evaluate((e) => e.closest('label')?.getBoundingClientRect().height ?? 0);
    expect(height, `${id} row height`).toBeGreaterThanOrEqual(32);
  }
});
