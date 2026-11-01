import { expect, test } from '@playwright/experimental-ct-react';
import { itemPeekTestIds } from '../../itemPeek/testIds';
import { retroInsights, stubRetros } from '../ct/retroFixtures';
import { retrosTestIds as ids } from '../testIds';
import { insightsProject } from './ct/insightsProject';
import { RecurringThemes } from './RecurringThemes';

const open = { hooksConfig: { route: '/p/SHIP/retros/insights' } };

test('RecurringThemes_WhenMounted_ShouldListRowsInFixtureOrderWithACellPerSprint', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(<RecurringThemes project={insightsProject} insights={retroInsights} />, open);
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
  await mount(<RecurringThemes project={insightsProject} insights={retroInsights} />, open);
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
  await mount(<RecurringThemes project={insightsProject} insights={retroInsights} />, open);
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
  await mount(<RecurringThemes project={insightsProject} insights={retroInsights} />, open);
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
  await mount(<RecurringThemes project={insightsProject} insights={retroInsights} />, open);
  const cell = page.getByTestId(ids.retroThemeRename).first();
  await cell.locator('[data-action="edit"]').click();
  await cell.locator('[data-field="name"]').fill('Brief too vague');
  await cell.locator('[data-action="save"]').click();
  await expect(cell.locator('[data-error]')).toHaveText('That name is taken');
});

test('RecurringThemes_WhenThemesRecur_ShouldWarnWithTheCount', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<RecurringThemes project={insightsProject} insights={retroInsights} />, open);
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
  await mount(<RecurringThemes project={insightsProject} insights={calm} />, open);
  await expect(page.locator('[data-callout="recurring"]')).toHaveCount(0);
});

test('RecurringThemes_WhenMounted_ShouldShowTheSprintRangeInTheSubtitle', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(<RecurringThemes project={insightsProject} insights={retroInsights} />, open);
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
    const view = await mount(
      <RecurringThemes project={insightsProject} insights={retroInsights} />,
      open,
    );
    await expect(view).toHaveScreenshot(`recurring-themes.${theme}.png`, {
      animations: 'disabled',
    });
  });
}
