import { expect, test } from '@playwright/experimental-ct-react';
import type { Locator, Page } from 'playwright';
import { AppRoutes } from '../../../../AppRoutes';
import { stubShell } from '../../../../pages/screenshots/shellStub';
import { touchTestIds } from '../../../../touch/testIds';
import { retroSharedTestIds as shared } from '../../../retroShared/testIds';
import { stubRetros } from '../../ct/retroFixtures';
import { retrosTestIds as ids } from '../../testIds';

async function open(
  page: Page,
  mount: (c: React.JSX.Element, o: object) => Promise<unknown>,
  query = '',
) {
  await stubShell(page);
  await stubRetros(page);
  await mount(<AppRoutes />, {
    hooksConfig: { route: `/p/SHIP/sprints/sprint-24/retro${query}` },
  });
  await expect(page.getByTestId(shared.retroNote).first()).toBeVisible();
}

// A section inside the sheet: every <p> shows all its text, and nothing sticks out of the sheet body sideways.
async function expectNothingClipped(section: Locator, sheetBody: Locator) {
  const clipped = await section.evaluate(
    (node) =>
      [...node.querySelectorAll('p')].filter(
        (p) => p.scrollHeight > p.clientHeight + 1 || p.scrollWidth > p.clientWidth + 1,
      ).length,
  );
  expect(clipped).toBe(0);
  const body = (await sheetBody.boundingBox())!;
  for (const part of await section.locator('p').all()) {
    const box = (await part.boundingBox())!;
    expect(box.x).toBeGreaterThanOrEqual(body.x);
    expect(box.x + box.width).toBeLessThanOrEqual(body.x + body.width + 1);
  }
}

test('RetroRailSheet_Rows_ShouldBeAtLeast32px', async ({ mount, page }) => {
  await open(page, mount);
  const sections = [
    { open: ids.retroNumberRetros, section: ids.retroCoverage, row: ids.retroCoverageRow, min: 40 },
    { open: ids.retroNumberActions, section: ids.retroRailActions, row: ids.retroRailRow, min: 40 },
    {
      open: ids.retroNumberRecurring,
      section: ids.retroRailRecurring,
      row: ids.retroRailRow,
      min: 40,
    },
  ];
  for (const { open: trigger, section, row, min } of sections) {
    await page.getByTestId(trigger).tap();
    const sheet = page.getByTestId(touchTestIds.sheet);
    const rows = page.getByTestId(section).getByTestId(row);
    await expect(rows.first()).toBeVisible();
    for (const r of await rows.all()) {
      const box = (await r.boundingBox())!;
      expect(box.height).toBeGreaterThanOrEqual(min);
    }
    // The sheet's title names the section: no repeated uppercase heading, so the first thing in the section is the hint.
    const heading = await page
      .getByTestId(section)
      .evaluate((node) => (node.firstElementChild as HTMLElement).tagName);
    expect(heading).toBe('P');
    await expectNothingClipped(page.getByTestId(section), page.getByTestId(touchTestIds.sheetBody));
    // Dividers: every row draws a bottom border.
    const borders = await rows.evaluateAll((nodes) =>
      nodes.map((n) => getComputedStyle(n).borderBottomWidth),
    );
    for (const width of borders) expect(width).not.toBe('0px');
    await sheet.getByTestId(touchTestIds.sheetClose).tap();
    await expect(sheet).toHaveCount(0);
  }
});

test('RetroCoverageSheet_WhenRowIsWaiting_ShouldExplainWaitingInText', async ({ mount, page }) => {
  await open(page, mount);
  await page.getByTestId(ids.retroNumberRetros).tap();
  await expect(page.getByTestId(ids.retroCoverageRow).first()).toBeVisible();
  await expect(page.getByTestId(ids.retroCoverage)).toContainText(
    'Waiting: the retro is logged when its last story closes.',
  );
});

test('RetroRecurringSheet_ShouldOfferOpenInsights', async ({ mount, page }) => {
  await open(page, mount);
  await page.getByTestId(ids.retroNumberRetros).tap();
  await expect(page.getByTestId(ids.retroCoverage)).toBeVisible();
  await expect(page.getByTestId(ids.retroRecurringOpenInsights)).toHaveCount(0);
  await page.getByTestId(touchTestIds.sheetClose).tap();
  await page.getByTestId(ids.retroNumberRecurring).tap();
  const button = page.getByTestId(ids.retroRecurringOpenInsights);
  await expect(button).toBeVisible();
  const box = (await button.boundingBox())!;
  const footer = (await page.getByTestId(touchTestIds.sheetFooter).boundingBox())!;
  expect(box.height).toBeGreaterThanOrEqual(32);
  // Full width inside the footer's 12px padding.
  expect(box.width).toBeGreaterThanOrEqual(footer.width - 26);
});

test('RetroFilterSheet_ShouldPutLabelsAboveFullWidthSegments', async ({ mount, page }) => {
  await open(page, mount);
  await page.getByTestId(ids.retroFilterOpen).tap();
  const body = (await page.getByTestId(touchTestIds.sheetBody).boundingBox())!;
  for (const group of [ids.retroWrittenByFilter, ids.retroToDecideFilter]) {
    const segments = page.getByTestId(group).getByRole('button');
    const boxes = await Promise.all((await segments.all()).map((b) => b.boundingBox()));
    for (const box of boxes) expect(box!.height).toBeGreaterThanOrEqual(32);
    const left = Math.min(...boxes.map((b) => b!.x));
    const right = Math.max(...boxes.map((b) => b!.x + b!.width));
    // The segments span the sheet body (less its 12px side padding).
    expect(right - left).toBeGreaterThanOrEqual(body.width - 28);
    // Equal widths.
    const widths = boxes.map((b) => b!.width);
    expect(Math.max(...widths) - Math.min(...widths)).toBeLessThanOrEqual(1);
  }
  // The labels sit on their own line, above the segments.
  const written = page.getByTestId(ids.retroWrittenByFilter);
  const label = (await written.getByText('Written by').boundingBox())!;
  const first = (await written.getByRole('button').first().boundingBox())!;
  expect(label.y + label.height).toBeLessThanOrEqual(first.y + 1);
  const points = await page
    .getByTestId(touchTestIds.sheetBody)
    .getByText('Points', { exact: true })
    .boundingBox();
  const decide = (await page.getByTestId(ids.retroToDecideFilter).boundingBox())!;
  expect(points!.y + points!.height).toBeLessThanOrEqual(decide.y + 1);
  for (const id of [ids.retroFilterClear, ids.retroFilterShow]) {
    expect((await page.getByTestId(id).boundingBox())!.height).toBeGreaterThanOrEqual(32);
  }
  // The hint wraps inside the sheet.
  const hint = page.getByTestId(touchTestIds.sheetBody).getByText('To decide hides Went well');
  const hintBox = (await hint.boundingBox())!;
  expect(hintBox.x + hintBox.width).toBeLessThanOrEqual(body.x + body.width + 1);
  expect(await hint.evaluate((p) => p.scrollHeight <= p.clientHeight + 1)).toBe(true);
});

test('SprintRetroMore_ShouldBeTitledWithTheSprintAndGroupGoTo', async ({ mount, page }) => {
  await open(page, mount);
  await page.getByTestId(ids.retroToolbar).getByTestId(touchTestIds.moreButton).tap();
  await expect(page.getByTestId(touchTestIds.sheetTitle)).toHaveText('Sprint 24 retro');
  const rows = page.getByTestId(touchTestIds.actionSheetRow);
  await expect(rows).toHaveCount(5);
  await expect(rows.nth(0)).toContainText('with a retro');
  await expect(rows.nth(1)).toContainText('new');
  await expect(page.getByTestId(touchTestIds.sheet)).toContainText('Go to');
});

test('NewActionItem_Checkboxes_ShouldBeTouchSized', async ({ mount, page }) => {
  await open(page, mount, '?cat=wentBadly');
  await page.getByTestId(shared.retroNoteAddAction).first().tap();
  await expect(page.getByTestId(shared.retroActionDialog)).toBeVisible();
  const picks = page.getByTestId(shared.retroActionPick);
  await expect(picks.first()).toBeVisible();
  for (const pick of await picks.all()) {
    const row = (await pick.boundingBox())!;
    // The whole row is the tap target (a label).
    expect(row.height).toBeGreaterThanOrEqual(40);
    const check = (await pick.getByTestId(shared.retroActionPickCheck).boundingBox())!;
    expect(check.width).toBeGreaterThanOrEqual(20);
    expect(check.height).toBeGreaterThanOrEqual(20);
  }
});
