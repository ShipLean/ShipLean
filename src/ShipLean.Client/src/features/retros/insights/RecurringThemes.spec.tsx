import { expect, test } from '@playwright/experimental-ct-react';
import type { RetroInsightsDto } from '@shiplean/core/api/retros';
import { itemPeekTestIds } from '../../itemPeek/testIds';
import { insightsWeeks, retroInsights, stubRetros } from '../ct/retroFixtures';
import { retrosTestIds as ids } from '../testIds';
import { insightsProject } from './ct/insightsProject';
import { RetroActionDialogProvider } from '../../retroShared/actionDialog/RetroActionDialogProvider';
import { retroSharedTestIds } from '../../retroShared/testIds';
import { RecurringThemes } from './RecurringThemes';

const table = (insights: RetroInsightsDto) => (
  <RetroActionDialogProvider projectId={insightsProject.id}>
    <RecurringThemes project={insightsProject} insights={insights} />
  </RetroActionDialogProvider>
);

const open = { hooksConfig: { route: '/p/SHIP/retros/insights' } };

test('RecurringThemes_WhenMounted_ShouldListRowsInFixtureOrderWithACellPerSprint', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(table(retroInsights), open);
  const rows = page.getByTestId(ids.retroRecurringRow);
  await expect(rows).toHaveCount(retroInsights.themes.length);
  const ids_ = await rows.evaluateAll((els) => els.map((e) => e.getAttribute('data-theme-id')));
  expect(ids_).toEqual(retroInsights.themes.map((t) => t.theme.id));
  for (const sprint of retroInsights.sprints) {
    await expect(
      rows.first().locator(`[data-cell="mentions"][data-sprint-id="${sprint.id}"]`),
    ).toHaveCount(1);
  }
  await expect(rows.first().locator('[data-cell="mentions"]')).toHaveCount(
    retroInsights.sprints.length,
  );
  await expect(rows.first().locator('[data-cell="total"]')).toHaveText(
    String(retroInsights.themes[0].total),
  );
});

test('RecurringThemes_WhenStatusesDiffer_ShouldLabelEachOfTheFour', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(table(retroInsights), open);
  const labels = {
    recurring: 'Recurring',
    beingAddressed: 'Being addressed',
    resolved: 'Resolved',
    quiet: 'Quiet',
  };
  for (const [status, label] of Object.entries(labels)) {
    const row = page
      .getByTestId(ids.retroRecurringRow)
      .and(page.locator(`[data-status="${status}"]`))
      .first();
    await expect(row.locator(`span[data-status="${status}"]`)).toHaveText(label);
  }
});

test('RecurringThemes_WhenRowHasActions_ShouldLinkEachKeyToTheItem', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(table(retroInsights), open);
  const first = page.getByTestId(ids.retroRecurringRow).first();
  const links = first.getByTestId(itemPeekTestIds.link);
  await expect(links).toHaveCount(retroInsights.themes[0].actionItems.length);
  await expect(links.first()).toHaveAttribute(
    'href',
    new RegExp(`${retroInsights.themes[0].actionItems[0].key}$`),
  );
});

test('RecurringThemes_WhenRenamed_ShouldPutTheNewName', async ({ mount, page }) => {
  const stub = await stubRetros(page);
  await mount(table(retroInsights), open);
  const cell = page.getByTestId(ids.retroThemeRename).first();
  await cell.locator('[data-action="edit"]').click();
  await cell.locator('[data-field="name"]').fill('Baselines come from CI');
  await cell.locator('[data-action="save"]').click();
  await expect.poll(() => stub.writes().length).toBe(1);
  const write = stub.writes()[0];
  expect(write.method).toBe('PUT');
  expect(write.path).toBe(`/api/retros/themes/${retroInsights.themes[0].theme.id}`);
  expect(write.body).toEqual({ name: 'Baselines come from CI' });
});

test('RecurringThemes_WhenRenameSaved_ShouldCloseTheEditField', async ({ mount, page }) => {
  const stub = await stubRetros(page);
  await mount(table(retroInsights), open);
  const cell = page.getByTestId(ids.retroThemeRename).first();
  await cell.locator('[data-action="edit"]').click();
  await cell.locator('[data-field="name"]').fill('Baselines come from CI');
  await cell.locator('[data-action="save"]').click();
  await expect.poll(() => stub.writes().length).toBe(1);
  await expect(cell.locator('[data-field="name"]')).toHaveCount(0);
  await expect(cell.locator('[data-action="edit"]')).toBeVisible();
});

test('RecurringThemes_WhenRenameConflicts_ShouldSayTheNameIsTaken', async ({ mount, page }) => {
  await stubRetros(page);
  await page.route(
    (url) => /^\/api\/retros\/themes\/[^/]+$/.test(url.pathname),
    (route) =>
      route.fulfill({
        status: 409,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'name taken', code: 'conflict', field: 'name' }),
      }),
  );
  await mount(table(retroInsights), open);
  const cell = page.getByTestId(ids.retroThemeRename).first();
  await cell.locator('[data-action="edit"]').click();
  await cell.locator('[data-field="name"]').fill('Brief too vague');
  await cell.locator('[data-action="save"]').click();
  await expect(cell.locator('[data-error]')).toHaveText('That name is taken');
});

test('RecurringThemes_WhenThemesRecur_ShouldWarnWithTheCount', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(table(retroInsights), open);
  const count = retroInsights.themes.filter((t) => t.status === 'recurring').length;
  expect(count).toBeGreaterThan(0);
  await expect(page.locator('[data-callout="recurring"]')).toContainText(`${count} `);
});

test('RecurringThemes_WhenNoThemeRecurs_ShouldHideTheCallout', async ({ mount, page }) => {
  await stubRetros(page);
  const calm = {
    ...retroInsights,
    themes: retroInsights.themes.map((t) => ({ ...t, status: 'quiet' as const })),
  };
  await mount(table(calm), open);
  await expect(page.locator('[data-callout="recurring"]')).toHaveCount(0);
});

test('RecurringThemes_WhenMounted_ShouldShowTheSprintRangeInTheSubtitle', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(table(retroInsights), open);
  const first = retroInsights.sprints[0].name;
  const last = retroInsights.sprints[retroInsights.sprints.length - 1].name.replace(
    /^Sprint\s+/,
    '',
  );
  await expect(page.getByText(`${first} – ${last}`)).toBeVisible();
});

for (const theme of ['light', 'dark'] as const) {
  test(`RecurringThemes_WhenRendered_ShouldMatchItsScreenshot_${theme}`, async ({
    mount,
    page,
  }) => {
    await stubRetros(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    const view = await mount(table(retroInsights), open);
    await expect(view).toHaveScreenshot(`recurring-themes.${theme}.png`, {
      animations: 'disabled',
    });
  });
}

test('RecurringThemes_WhenModeIsWeek_ShouldCarryItToTheStatusAndVerdictChips', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(table(insightsWeeks), open);
  const rows = page.getByTestId(ids.retroRecurringRow);
  await expect(rows.first().locator('span[data-status][data-mode="week"]')).toHaveCount(1);
  await expect(rows.first().locator('span[data-verdict][data-mode="week"]')).toHaveCount(1);
  await expect(page.locator('[data-mode="sprint"]')).toHaveCount(0);
  await expect(page.getByTestId(ids.retroTipStatusHead)).toHaveAttribute('data-mode', 'week');
  await expect(page.getByTestId(ids.retroTipVerdictHead)).toHaveAttribute('data-mode', 'week');
});

const header = (page: Page) => page.locator('thead th');
type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];
const headLabels = (page: Page) => header(page).allTextContents();
const withNoSprint = (counts: number[]): RetroInsightsDto => ({
  ...retroInsights,
  themes: retroInsights.themes.map((t, i) => ({
    ...t,
    counts,
    noSprint: i === 0 ? 2 : 0,
    total: counts.reduce((a, b) => a + b, 0) + (i === 0 ? 2 : 0),
  })),
  unthemed: { counts: [1, 0, 0, 0, 0, 2], noSprint: 3, total: 6 },
});

test('RecurringThemes_WhenModeIsWeek_ShouldShowWeekColumnsAndNoNoSprintColumn', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(table(insightsWeeks), open);
  const labels = await headLabels(page);
  expect(labels.slice(1, 7)).toEqual(insightsWeeks.columns.map((c) => c.label));
  await expect(page.getByTestId(ids.retroInsightsNosprint)).toHaveCount(0);
  await expect(
    page.getByTestId(ids.retroRecurringRow).first().locator('[data-cell="no-sprint"]'),
  ).toHaveCount(0);
  await expect(
    page.getByTestId(ids.retroInsightsUnthemed).locator('[data-cell="no-sprint"]'),
  ).toHaveCount(0);
  await expect(page.getByRole('columnheader', { name: '26 Oct' })).toBeVisible();
});

test('RecurringThemes_WhenModeIsSprint_ShouldShowNoSprintBeforeTotal', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(table(withNoSprint([1, 0, 0, 0, 0, 1])), open);
  const labels = (await headLabels(page)).map((l) => l.trim());
  const at = labels.findIndex((l) => l.startsWith('No sprint'));
  expect(at).toBe(retroInsights.columns.length + 1);
  expect(labels[at + 1]).toBe('Total');
  const row = page.getByTestId(ids.retroRecurringRow).first();
  const cells = await row
    .locator('td')
    .evaluateAll((els) => els.map((e) => e.getAttribute('data-cell')));
  expect(cells.indexOf('no-sprint')).toBe(retroInsights.columns.length + 1);
  expect(cells.indexOf('total')).toBe(retroInsights.columns.length + 2);
});

test('RecurringThemes_WhenThemeHasNoSprintMentions_ShouldCountThemInTotal', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  const insights = withNoSprint([1, 0, 0, 0, 0, 1]);
  await mount(table(insights), open);
  const row = page.getByTestId(ids.retroRecurringRow).first();
  await expect(row.locator('[data-cell="no-sprint"]')).toHaveText('2');
  await expect(row.locator('[data-cell="total"]')).toHaveText(String(insights.themes[0].total));
  expect(insights.themes[0].total).toBe(4);
});

test('RecurringThemes_WhenUnthemedHasPoints_ShouldBeLastAndLinkToSearch', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(table(withNoSprint([1, 0, 0, 0, 0, 1])), open);
  const last = page.locator('tbody tr').last();
  await expect(last).toHaveAttribute('data-testid', ids.retroInsightsUnthemed);
  await expect(last).toContainText('not judged');
  await expect(last.locator('[data-cell="total"]')).toHaveText('6');
  await expect(last.getByRole('link')).toHaveAttribute('href', '/p/SHIP/retros?unthemed=true');
});

test('RecurringThemes_WhenUnthemedIsEmpty_ShouldHideTheRow', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(
    table({ ...retroInsights, unthemed: { counts: [0, 0, 0, 0, 0, 0], noSprint: 0, total: 0 } }),
    open,
  );
  await expect(page.getByTestId(ids.retroInsightsUnthemed)).toHaveCount(0);
});

test('RecurringThemes_WhenThemeRowActionClicked_ShouldOpenTheDialogWithTheTheme', async ({
  mount,
  page,
}) => {
  const stub = await stubRetros(page);
  await mount(table(retroInsights), open);
  const buttons = page.getByTestId(ids.retroInsightsThemeAction);
  await expect(buttons).toHaveCount(retroInsights.themes.length);
  await buttons.nth(1).click();
  await expect(page.getByTestId(retroSharedTestIds.retroActionDialog)).toBeVisible();
  await expect
    .poll(() => stub.reads('/retros/action-dialog').map((c) => c.query))
    .toContainEqual(expect.stringContaining(`theme=${retroInsights.themes[1].theme.id}`));
});

test('RecurringThemes_WhenNoSprintAndUnthemedShow_ShouldMountTheirTipsWithEachLabelOnce', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(table(withNoSprint([1, 0, 0, 0, 0, 1])), open);
  const head = page.getByTestId(ids.retroInsightsNosprint);
  await expect(head.getByTestId(ids.retroTipNosprint)).toHaveCount(1);
  await expect(head.getByText('No sprint')).toHaveCount(1);
  const row = page.getByTestId(ids.retroInsightsUnthemed);
  await expect(row.getByTestId(ids.retroTipUnthemed)).toHaveCount(1);
  await expect(row.getByText('Unthemed')).toHaveCount(1);
});

for (const [name, insights, expected, absent] of [
  ['Week', insightsWeeks, `Counted over the last ${insightsWeeks.columns.length} weeks`, 'sprints'],
  ['Sprint', retroInsights, 'in several recent sprints', 'Counted over'],
] as const) {
  test(`RecurringThemes_When${name}Mode_ShouldWordTheCalloutInThatUnit`, async ({
    mount,
    page,
  }) => {
    await stubRetros(page);
    await mount(table(insights), open);
    const callout = page.locator('[data-callout="recurring"]');
    await expect(callout).toContainText(expected);
    await expect(callout).not.toContainText(absent);
  });
}

test('RecurringThemes_WhenWeek12ColumnsShown_ShouldStillSayLast6Weeks', async ({ mount, page }) => {
  await stubRetros(page);
  const base = insightsWeeks;
  const columns = Array.from({ length: 12 }, (_, i) => ({
    ...base.columns[i % base.columns.length],
    key: `2026-08-${String(10 + i).padStart(2, '0')}`,
    isCurrent: i === 11,
  }));
  const pad = (counts: number[]) => Array.from({ length: 12 }, (_, i) => counts[i % counts.length]);
  const wide = {
    ...base,
    range: 'w12' as const,
    columns,
    themes: base.themes.map((t) => ({ ...t, counts: pad(t.counts) })),
    unthemed: { ...base.unthemed, counts: pad(base.unthemed.counts) },
  };
  await mount(table(wide), open);
  const callout = page.locator('[data-callout="recurring"]');
  await expect(callout).toContainText('the last 6 weeks');
  await expect(callout).not.toContainText('12 weeks');
});
