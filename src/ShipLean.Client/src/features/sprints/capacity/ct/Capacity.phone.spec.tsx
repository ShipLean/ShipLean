import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../../../AppRoutes';
import { PEOPLE, stubShell } from '../../../../pages/screenshots/shellStub';
import {
  SPRINT_24,
  capacitySprint24,
  emptyCapacity,
  sprintsWithGoals,
  stubCapacity,
} from '../../../../pages/screenshots/fixtures/sprints';
import { componentTestIds } from '../../../../components/testIds';
import { touchTestIds } from '../../../../touch/testIds';
import { sprintsTestIds as ids } from '../../testIds';

// Sprint Capacity on a phone (SHIP-726, L726a). Runs in the CT phone project.

const route = `/p/SHIP/sprints/${SPRINT_24}/capacity`;

const open = async (
  page: Parameters<typeof stubShell>[0],
  mount: (c: React.JSX.Element, o: { hooksConfig: { route: string } }) => Promise<unknown>,
) => {
  await stubShell(page, { sprints: sprintsWithGoals });
  await stubCapacity(page);
  await mount(<AppRoutes />, { hooksConfig: { route } });
  await expect(page.getByTestId(ids.capacityRow)).toHaveCount(5);
  await expect(page.getByTestId(ids.capacityCallout)).toBeVisible();
};

test('Capacity_WhenOnPhone_ShouldShowTilesAboveMemberRowsWithoutSidewaysScroll', async ({
  mount,
  page,
}) => {
  await open(page, mount);

  const tiles = [ids.capacityKpiCapacity, ids.capacityKpiCommitted, ids.capacityKpiVelocity];
  const boxes = [];
  for (const id of tiles) boxes.push((await page.getByTestId(id).boundingBox())!);
  const firstRow = (await page.getByTestId(ids.capacityRow).first().boundingBox())!;
  // One row of three tiles, all above the first member.
  expect(new Set(boxes.map((b) => Math.round(b.y))).size).toBe(1);
  expect(boxes[0]!.x).toBeLessThan(boxes[1]!.x);
  expect(boxes[1]!.x).toBeLessThan(boxes[2]!.x);
  for (const box of boxes) expect(box.y + box.height).toBeLessThan(firstRow.y);
  // The days line sits under the name and points.
  const name = (await page.getByTestId(ids.capacityPoints).first().boundingBox())!;
  const days = (await page.getByTestId(ids.capacityRowDays).first().boundingBox())!;
  expect(days.y).toBeGreaterThan(name.y + name.height - 1);
  // Compared on the document element: a mobile context widens innerWidth to fit overflow.
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
  expect(overflow).toBeLessThanOrEqual(0);
  // And the table's own scroll box: rows, not a table that scrolls sideways inside its wrapper.
  const wrapper = await page
    .getByTestId(ids.capacityTable)
    .evaluate((el) => el.scrollWidth - el.clientWidth);
  expect(wrapper).toBeLessThanOrEqual(0);
  // The toolbar's own Save is not shown at touch.
  await expect(page.getByTestId(ids.capacitySave)).toHaveCount(0);
  await expect(page.getByTestId(touchTestIds.saveBar)).toHaveCount(0);
});

test('Capacity_WhenEditingDaysOnPhone_ShouldShowTheWholeNoteField', async ({ mount, page }) => {
  await open(page, mount);
  // The note is typed in the days sheet now, on a line of its own under the days and Use default. Inline on the row it
  // was cut off at the right edge (R1a); in the sheet it must still fit the sheet and show its whole placeholder.
  await page.getByTestId(ids.capacityRow).first().tap();
  const sheet = page.getByTestId(touchTestIds.sheet);
  await sheet.getByTestId(ids.capacityDays).fill('6');
  await expect(sheet.getByTestId(ids.capacityDaysReset)).toBeVisible();

  const note = sheet.getByTestId(ids.capacityNote);
  const sheetBox = (await sheet.boundingBox())!;
  const noteBox = (await note.boundingBox())!;
  expect(noteBox.x).toBeGreaterThanOrEqual(sheetBox.x);
  expect(noteBox.x + noteBox.width).toBeLessThanOrEqual(sheetBox.x + sheetBox.width);
  // On a line of its own, under the days field and Use default.
  const resetBox = (await sheet.getByTestId(ids.capacityDaysReset).boundingBox())!;
  expect(noteBox.y).toBeGreaterThanOrEqual(resetBox.y + resetBox.height);
  // Its placeholder fits inside the field, not cut off at the right edge.
  const room = await note.evaluate((el) => {
    const input = el as HTMLInputElement;
    const style = getComputedStyle(input);
    const context = document.createElement('canvas').getContext('2d')!;
    context.font = style.font;
    const text = context.measureText(input.placeholder).width;
    const inner =
      input.clientWidth - parseFloat(style.paddingLeft) - parseFloat(style.paddingRight);
    return inner - text;
  });
  expect(room).toBeGreaterThanOrEqual(0);
});

test('Capacity_WhenOnPhone_ShouldShowEachPersonAsATwoLineRowWithADaysBar', async ({
  mount,
  page,
}) => {
  await open(page, mount);
  // ND2-2: no fields on the row; a bar of days out of the working days under the name, the days in words beside it.
  const people = page.getByTestId(ids.capacityTable);
  await expect(people.getByTestId(ids.capacityDays)).toHaveCount(0);
  await expect(people.getByTestId(ids.capacityNote)).toHaveCount(0);
  const priya = page.getByTestId(ids.capacityRow).first();
  await expect(priya.getByTestId(ids.capacityRowDays)).toHaveText('10 / 10 days · default');
  const ana = page.getByTestId(ids.capacityRow).nth(4);
  await expect(ana.getByTestId(ids.capacityRowDays)).toHaveText('5 / 10 days');
  await expect(ana.getByTestId(ids.capacityRowNote)).toHaveText('Part-time this sprint');
  const bar = ana.getByTestId(componentTestIds.capacityBar);
  await expect(bar).toHaveAttribute('aria-valuenow', '5');
  await expect(bar).toHaveAttribute('aria-valuemax', '10');
  // Two lines: the bar sits under the name and points, the days beside the bar.
  const points = (await ana.getByTestId(ids.capacityPoints).boundingBox())!;
  const barBox = (await bar.boundingBox())!;
  const days = (await ana.getByTestId(ids.capacityRowDays).boundingBox())!;
  expect(barBox.y).toBeGreaterThanOrEqual(points.y + points.height - 1);
  expect(barBox.x + barBox.width).toBeLessThan(days.x);
  // Each bar has the same track, so 5 of 10 days fills half of it.
  const fill = (await ana.getByTestId(componentTestIds.capacityBarFill).boundingBox())!;
  expect(Math.abs(fill.width - barBox.width / 2)).toBeLessThanOrEqual(1);
  const priyaBar = (await priya.getByTestId(componentTestIds.capacityBar).boundingBox())!;
  expect(priyaBar.width).toBe(barBox.width);
  // ND2-7: a People header with the count and the team total above the rows.
  const head = page.getByTestId(ids.capacityPeopleHead);
  await expect(head).toContainText('People 5');
  await expect(head).toContainText('Team capacity 21.5 pts');
  const headBox = (await head.boundingBox())!;
  expect(headBox.y + headBox.height).toBeLessThanOrEqual((await priya.boundingBox())!.y);
});

test('Capacity_WhenRowTappedOnPhone_ShouldStepDaysByHalfADayInTheDaysSheet', async ({
  mount,
  page,
}) => {
  await open(page, mount);
  // ND2-4: Remove lives in the sheet, not on the row.
  await expect(page.getByTestId(ids.capacityRemove)).toHaveCount(0);
  const ana = page.getByTestId(ids.capacityRow).nth(4);
  await ana.tap();

  const sheet = page.getByTestId(touchTestIds.sheet);
  await expect(sheet.getByTestId(touchTestIds.sheetTitle)).toHaveText('Ana Costa');
  const days = sheet.getByTestId(ids.capacityDays);
  await expect(days).toHaveValue('5');
  await sheet.getByTestId(ids.capacityDaysMore).tap();
  await expect(days).toHaveValue('5.5');
  await expect(sheet.getByTestId(ids.capacitySheetPoints)).toHaveText('2.75');
  await sheet.getByTestId(ids.capacityDaysLess).tap();
  await sheet.getByTestId(ids.capacityDaysLess).tap();
  await expect(days).toHaveValue('4.5');
  await expect(ana.getByTestId(ids.capacityRowDays)).toHaveText('4.5 / 10 days');
  await expect(ana.getByTestId(ids.capacityUnsaved)).toHaveText('Unsaved');
  await expect(page.getByTestId(ids.capacityUnsaved)).toHaveCount(1);
  await expect(page.getByTestId(touchTestIds.saveBar)).toBeVisible();

  await sheet.getByTestId(ids.capacityDaysReset).tap();
  await expect(days).toHaveValue('');
  await expect(ana.getByTestId(ids.capacityRowDays)).toHaveText('10 / 10 days · default');

  await expect(sheet.getByTestId(ids.capacityRemove)).toContainText('Remove from Sprint 24');
  await sheet.getByTestId(ids.capacityRemove).tap();
  await expect(sheet).toHaveCount(0);
  await expect(page.getByTestId(ids.capacityRow)).toHaveCount(4);
  await expect(page.getByTestId(ids.capacityPeopleHead)).toContainText('People 4');
});

test('Capacity_WhenOnPhone_ShouldShowTheWholeVelocityTileLabel', async ({ mount, page }) => {
  await open(page, mount);
  // ND2-10: the third tile's label wraps inside its tile instead of ending in "Velocity (last …".
  const label = await page.getByTestId(ids.capacityKpiVelocity).evaluate((el) => {
    const next = el.nextElementSibling as HTMLElement;
    return { text: next.textContent, cut: next.scrollWidth > next.clientWidth };
  });
  expect(label).toEqual({ text: 'Velocity (last 3)', cut: false });
});

test('Capacity_WhenOnPhone_ShouldShowTeamVelocityOnOneRow', async ({ mount, page }) => {
  await open(page, mount);
  // ND2-6: label and hint on the left, a 96px field on the right of the same row (not a full-width field under the label).
  const layout = await page.getByTestId(ids.capacityVelocity).evaluate((el) => {
    const field = el.getBoundingClientRect();
    const label = el.previousElementSibling!.getBoundingClientRect();
    const hintId = el.getAttribute('aria-describedby')!.split(' ')[0]!;
    const hint = document.getElementById(hintId)!.getBoundingClientRect();
    return {
      width: Math.round(field.width),
      besideLabel: field.left >= label.right && field.top < label.bottom,
      besideHint: field.left >= hint.right && field.bottom > hint.top,
    };
  });
  expect(layout).toEqual({ width: 96, besideLabel: true, besideHint: true });
});

test('Capacity_WhenMoreTapped_ShouldOfferClear', async ({ mount, page }) => {
  await open(page, mount);

  await page.getByTestId(ids.capacityPhoneToolbar).getByTestId(touchTestIds.moreButton).tap();

  // ND2-8 (SHIP-1808 P5, capacity-more): one Add member row, off while everyone is in the table, then Clear.
  const sheet = page.getByTestId(touchTestIds.sheet);
  await expect(sheet.getByTestId(touchTestIds.sheetTitle)).toHaveText('Capacity');
  const rows = sheet.getByTestId(touchTestIds.actionSheetRow);
  await expect(rows).toHaveCount(2);
  await expect(rows.first()).toHaveAttribute('data-action-id', 'add');
  await expect(rows.first()).toHaveText('Add memberEveryone on the project is in the table›');
  await expect(rows.first()).toBeDisabled();
  await expect(rows.nth(1)).toHaveAttribute('data-action-id', 'clear');
  await expect(rows.nth(1)).toHaveText(
    'ClearRemove everyone; Sprint 24 plans against velocity (21)',
  );
  await expect(page.getByTestId(ids.capacityAddMember)).toHaveCount(0);
  await rows.nth(1).tap();
  await expect(page.getByTestId(ids.capacityRow)).toHaveCount(0);
  await expect(page.getByTestId(touchTestIds.saveBar)).toBeVisible();
});

test('Capacity_WhenCopyIsBlockedOnPhone_ShouldSayWhyInVisibleText', async ({ mount, page }) => {
  await stubShell(page, { sprints: sprintsWithGoals });
  await stubCapacity(page);
  await page.route('**/api/sprints/sprint-23/capacity', (r) =>
    r.fulfill({ json: { ...emptyCapacity, sprintId: 'sprint-23' } }),
  );
  await mount(<AppRoutes />, { hooksConfig: { route } });
  await expect(page.getByTestId(ids.capacityRow)).toHaveCount(5);

  await expect(page.getByTestId(ids.capacityCopy)).toBeDisabled();
  await expect(page.getByTestId(ids.capacityCopyBlocked)).toBeVisible();
  await expect(page.getByTestId(ids.capacityCopyBlocked)).toHaveText(
    'The sprint before has no capacity entered',
  );
});

test('Capacity_WhenATeammateChangedVelocityOnPhone_ShouldShowTheWholeConflictInTheSaveBar', async ({
  mount,
  page,
}) => {
  await open(page, mount);
  await page.getByTestId(ids.capacityVelocity).fill('31');
  await expect(page.getByTestId(touchTestIds.saveBar)).toBeVisible();
  await expect(page.getByTestId(ids.capacityConflict)).toHaveCount(0);

  const stored = await page.evaluate(() =>
    fetch('/api/sprints/sprint-24/capacity').then((r) => r.json()),
  );
  await page.route('**/api/sprints/sprint-24/capacity', (r) =>
    r.fulfill({ json: { ...stored, velocity: 28 } }),
  );
  await page.evaluate(() => window.dispatchEvent(new Event('visibilitychange')));

  const conflict = page.getByTestId(ids.capacityConflict);
  await expect(conflict).toHaveText('A teammate changed the velocity. Saving replaces it.');
  // Whole, not cut off: it wraps inside the bar and is not clipped by it.
  const bar = (await page.getByTestId(touchTestIds.saveBar).boundingBox())!;
  const text = (await conflict.boundingBox())!;
  expect(text.y).toBeGreaterThanOrEqual(bar.y);
  expect(text.y + text.height).toBeLessThanOrEqual(bar.y + bar.height);
  const clipped = await page
    .getByTestId(touchTestIds.saveBarMessage)
    .evaluate((el) => el.scrollWidth > el.clientWidth || el.scrollHeight > el.clientHeight);
  expect(clipped).toBe(false);
});

for (const theme of ['light', 'dark'] as const) {
  test(`Capacity_WhenOnPhone_ShouldMatchItsScreenshot_capacity-phone_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await open(page, mount);

    await expect(page.getByTestId(ids.capacity)).toHaveScreenshot(`capacity-phone.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`Capacity_WhenDirtyOnPhone_ShouldMatchItsScreenshot_capacity-phone-dirty_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await open(page, mount);

    await page.getByTestId(ids.capacityRow).first().tap();
    await page.getByTestId(touchTestIds.sheet).getByTestId(ids.capacityDays).fill('6');
    await page.getByTestId(touchTestIds.sheetClose).tap();

    await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
    await expect(page.getByTestId(touchTestIds.saveBar)).toBeVisible();
    await expect(page).toHaveScreenshot(`capacity-phone-dirty.${theme}.png`, {
      animations: 'disabled',
    });
  });
}

const withoutMei = {
  ...capacitySprint24,
  members: capacitySprint24.members.filter((m) => m.user.id !== PEOPLE.mei.id),
};

test('Capacity_WhenAddMemberPickedOnPhone_ShouldAddThemAndOpenTheirDaysSheet', async ({
  mount,
  page,
}) => {
  await stubShell(page, { sprints: sprintsWithGoals });
  await stubCapacity(page);
  await page.route(`**/api/sprints/${SPRINT_24}/capacity`, (r) => r.fulfill({ json: withoutMei }));
  await mount(<AppRoutes />, { hooksConfig: { route } });
  await expect(page.getByTestId(ids.capacityRow)).toHaveCount(4);

  // ND2-8: the toolbar ⋯ has one Add member row that opens a sheet of the members not in the table.
  await page.getByTestId(ids.capacityPhoneToolbar).getByTestId(touchTestIds.moreButton).tap();
  const add = page.getByTestId(touchTestIds.sheet).getByTestId(touchTestIds.actionSheetRow).first();
  await expect(add).toHaveText('Add member›');
  await add.tap();
  const sheet = page.getByTestId(touchTestIds.sheet);
  await expect(sheet.getByTestId(touchTestIds.sheetTitle)).toHaveText('Add member');
  const people = sheet.getByTestId(touchTestIds.actionSheetRow);
  await expect(people).toHaveCount(1);
  await expect(people).toHaveText('Mei LinStarts at the default, 10 days');

  // Picking her adds her row and opens her days sheet (SHIP-1808 capacity-add).
  await people.tap();
  await expect(sheet.getByTestId(touchTestIds.sheetTitle)).toHaveText('Mei Lin');
  await expect(sheet.getByTestId(ids.capacityDays)).toHaveValue('');
  await expect(page.getByTestId(ids.capacityRow)).toHaveCount(5);
  await expect(page.getByTestId(ids.capacityUnsaved)).toHaveCount(1);
  await sheet.getByTestId(touchTestIds.sheetClose).tap();
  // ND2-9: once nobody is left out, the foot of People has no + Add member.
  await expect(page.getByTestId(ids.capacityAddMember)).toHaveCount(0);
});

test('Capacity_WhenSomeoneIsOutOfTheTableOnPhone_ShouldOfferAddMemberAtTheFootOfPeople', async ({
  mount,
  page,
}) => {
  await stubShell(page, { sprints: sprintsWithGoals });
  await stubCapacity(page);
  await page.route(`**/api/sprints/${SPRINT_24}/capacity`, (r) => r.fulfill({ json: withoutMei }));
  await mount(<AppRoutes />, { hooksConfig: { route } });
  await expect(page.getByTestId(ids.capacityRow)).toHaveCount(4);

  // ND2-9 (P5): + Add member at the foot of People while anyone is out of the table.
  const foot = page.getByTestId(ids.capacityAddMember);
  await expect(foot).toHaveText('+ Add member');
  await foot.tap();
  const sheet = page.getByTestId(touchTestIds.sheet);
  await expect(sheet.getByTestId(touchTestIds.sheetTitle)).toHaveText('Add member');
  await expect(sheet.getByTestId(touchTestIds.actionSheetRow)).toHaveText(
    'Mei LinStarts at the default, 10 days',
  );
});

test('Capacity_WhenNobodyIsInTheTableOnPhone_ShouldShowTheEmptyStateWithAddMember', async ({
  mount,
  page,
}) => {
  await stubShell(page, { sprints: sprintsWithGoals });
  await stubCapacity(page);
  await page.route(`**/api/sprints/${SPRINT_24}/capacity`, (r) =>
    r.fulfill({ json: emptyCapacity }),
  );
  await mount(<AppRoutes />, { hooksConfig: { route } });

  // ND2-9 (capacity-empty): the People card says so, and + Add member offers everyone.
  await expect(page.getByTestId(ids.capacityPeopleHead)).toContainText('People 0');
  await expect(page.getByTestId(ids.capacityEmpty)).toHaveText(
    "No capacity enteredSprint 24 plans against velocity (21). Add the people who are in, or copy Sprint 23's table.",
  );
  await page.getByTestId(ids.capacityAddMember).tap();
  await expect(
    page.getByTestId(touchTestIds.sheet).getByTestId(touchTestIds.actionSheetRow),
  ).toHaveCount(5);
});

test('Capacity_WhenDraftChangedOnPhone_ShouldSayUnsavedCapacityChangesInTheSaveBar', async ({
  mount,
  page,
}) => {
  await open(page, mount);
  await page.getByTestId(ids.capacityRow).nth(4).tap();
  await page.getByTestId(touchTestIds.sheet).getByTestId(ids.capacityDaysMore).tap();
  await page.getByTestId(touchTestIds.sheetClose).tap();

  // SHIP-1808 P4: Unsaved capacity changes · Discard · Save.
  await expect(page.getByTestId(touchTestIds.saveBarMessage)).toHaveText(
    'Unsaved capacity changes',
  );
});

test('Capacity_WhenOnPhone_ShouldNameEachRowForScreenReadersWithoutABlockInsideAButton', async ({
  mount,
  page,
}) => {
  await open(page, mount);
  const row = await page
    .getByTestId(ids.capacityRow)
    .nth(4)
    .evaluate((el) => {
      const buttons = el.querySelectorAll('button');
      return {
        name: buttons[0]?.getAttribute('aria-label'),
        buttons: buttons.length,
        blocksInButtons: el.querySelectorAll('button div').length,
        coverHidden: [...el.children]
          .filter((child) => child.tagName !== 'BUTTON')
          .every((child) => child.getAttribute('aria-hidden') === 'true'),
      };
    });
  expect(row).toEqual({
    name: 'Ana Costa, 5 / 10 days, 2.5 pts, Part-time this sprint, edit days',
    buttons: 1,
    blocksInButtons: 0,
    coverHidden: true,
  });
});

test('Capacity_WhenTypedDaysAreInvalidInTheSheet_ShouldHideTheSumAndTurnOffTheSteps', async ({
  mount,
  page,
}) => {
  await open(page, mount);
  await page.getByTestId(ids.capacityRow).nth(4).tap();
  const sheet = page.getByTestId(touchTestIds.sheet);
  const days = sheet.getByTestId(ids.capacityDays);

  await days.fill('11');
  await expect(sheet.getByTestId(ids.capacitySheetPoints)).toHaveText('—');
  await expect(sheet.getByTestId(ids.capacitySheetFormula)).toHaveCount(0);
  await expect(sheet.getByTestId(ids.capacityDaysLess)).toBeDisabled();
  await expect(sheet.getByTestId(ids.capacityDaysMore)).toBeDisabled();
  await expect(days).toHaveAttribute('aria-invalid', 'true');
  await expect(days).toHaveValue('11');

  await days.fill('6');
  await expect(sheet.getByTestId(ids.capacitySheetFormula)).toHaveText(
    '25 velocity ÷ 5 people × 6 ÷ 10 days',
  );
  await expect(sheet.getByTestId(ids.capacityDaysMore)).toBeEnabled();
});
