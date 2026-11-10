import { expect, test } from '@playwright/experimental-ct-react';
import type { RetroActionDialogDto, RetroInsightsDto } from '@shiplean/core/api/retros';
import { componentTestIds } from '../../../components/testIds';
import { ToastHost } from '../../../components/ToastHost';
import { actionDialog, insightsWeeks, retroInsights, stubRetros } from '../ct/retroFixtures';
import { retroFixOptions } from '../../retroShared/actionDialog/retroFixTypes';
import { retrosTestIds as ids } from '../testIds';
import { insightsProject } from './ct/insightsProject';
import { RetroActionDialogProvider } from '../../retroShared/actionDialog/RetroActionDialogProvider';
import { retroSharedTestIds } from '../../retroShared/testIds';
import { ActionItemsTable } from './ActionItemsTable';
import { RetroInsightsPanel } from './RetroInsightsPanel';

const table = (insights: RetroInsightsDto) => (
  <RetroActionDialogProvider projectId={insightsProject.id}>
    <ActionItemsTable project={insightsProject} insights={insights} />
  </RetroActionDialogProvider>
);

type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];

const open = { hooksConfig: { route: '/p/SHIP/retros/insights?view=actions' } };

const sprintsOf = [
  { id: 'sprint-24', name: 'Sprint 24', status: 'current', startDate: '2026-10-05' },
  { id: 'sprint-26', name: 'Sprint 26', status: 'future', startDate: '2026-11-16' },
  { id: 'sprint-25', name: 'Sprint 25', status: 'future', startDate: '2026-11-02' },
];

interface Write {
  method: string;
  path: string;
  body: unknown;
}

async function stubWorkItems(page: Page, patchStatus = 200) {
  const writes: Write[] = [];
  await page.route(
    (url) => url.pathname === `/api/projects/${insightsProject.id}/sprints`,
    (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(sprintsOf),
      }),
  );
  await page.route(
    (url) => /\/work-items\/by-key\/[^/]+$/.test(url.pathname),
    (route) => {
      const key = new URL(route.request().url()).pathname.split('/').pop();
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ id: `id-${key}`, key, revision: 7 }),
      });
    },
  );
  await page.route(
    (url) => /^\/api\/work-items\/[^/]+$/.test(url.pathname),
    (route) => {
      const request = route.request();
      writes.push({
        method: request.method(),
        path: new URL(request.url()).pathname,
        body: request.postDataJSON() as unknown,
      });
      return patchStatus === 200
        ? route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({ id: 'x', key: 'x', revision: 8, notices: [] }),
          })
        : route.fulfill({
            status: patchStatus,
            contentType: 'application/json',
            body: JSON.stringify({ error: 'Could not move it', code: 'validation' }),
          });
    },
  );
  return writes;
}

test('ActionItemsTable_WhenMounted_ShouldListRowsInFixtureOrder', async ({ mount, page }) => {
  await stubRetros(page);
  await stubWorkItems(page);
  await mount(table(retroInsights), open);
  const rows = page.getByTestId(ids.retroActionRow);
  await expect(rows).toHaveCount(retroInsights.actionItems.length);
  const keys = await rows.evaluateAll((els) => els.map((e) => e.getAttribute('data-item-key')));
  expect(keys).toEqual(retroInsights.actionItems.map((r) => r.action.key));
  const first = retroInsights.actionItems[0];
  const row = rows.first();
  await expect(row.locator('[data-cell="theme"]')).toContainText(first.theme!.name);
  await expect(row.locator('[data-cell="raised"]')).toHaveText(first.raisedIn!.name);
  await expect(row.locator('[data-cell="state"]')).toHaveText(first.action.state.name);
  await expect(row.locator('[data-cell="points"]')).toHaveText(String(first.action.points));
});

test('ActionItemsTable_WhenOwnerAndImpactAreNull_ShouldShowDashes', async ({ mount, page }) => {
  await stubRetros(page);
  await stubWorkItems(page);
  await mount(table(retroInsights), open);
  const row = page.getByTestId(ids.retroActionRow).first();
  await expect(row.locator('[data-cell="owner"]')).toHaveText('—');
  await expect(row.locator('[data-cell="impact"]')).toHaveText('—');
});

test('ActionItemsTable_WhenOwnerAndImpactSet_ShouldShowNameAndVerdict', async ({ mount, page }) => {
  await stubRetros(page);
  await stubWorkItems(page);
  await mount(table(retroInsights), open);
  const index = retroInsights.actionItems.findIndex((r) => r.owner && r.impact);
  const row = page.getByTestId(ids.retroActionRow).nth(index);
  await expect(row.locator('[data-cell="owner"]')).toContainText(
    retroInsights.actionItems[index].owner!.name,
  );
  await expect(row.locator('[data-cell="impact"] [data-verdict]')).toHaveAttribute(
    'data-verdict',
    retroInsights.actionItems[index].impact!,
  );
});

test('ActionItemsTable_WhenItemHasNoSprint_ShouldOfferTheNextSprint', async ({ mount, page }) => {
  await stubRetros(page);
  await stubWorkItems(page);
  await mount(table(retroInsights), open);
  const unplanned = retroInsights.actionItems.filter(
    (r) => r.action.sprint === null && r.action.state.category === 'proposed',
  );
  const pulls = page.getByTestId(ids.retroActionPull);
  await expect(pulls).toHaveCount(unplanned.length);
  await expect(pulls.first()).toHaveText('Pull into Sprint 25');
});

test('ActionItemsTable_WhenPulled_ShouldPatchTheSprintAndRefetch', async ({ mount, page }) => {
  const stub = await stubRetros(page);
  const writes = await stubWorkItems(page);
  await mount(<RetroInsightsPanel project={insightsProject} />, open);
  const pull = page.getByTestId(ids.retroActionPull).first();
  const key = (await pull.getAttribute('data-item-key'))!;
  await expect.poll(() => stub.reads('/retros/insights').length).toBe(1);
  await pull.click();
  await expect.poll(() => writes.length).toBe(1);
  expect(writes[0].method).toBe('PATCH');
  expect(writes[0].path).toBe(`/api/work-items/id-${key}`);
  const body = writes[0].body as { expectedRevision: number; changes: { sprint: unknown } };
  expect(body.expectedRevision).toBe(7);
  expect(body.changes.sprint).toEqual({ value: 'sprint-25' });
  await expect.poll(() => stub.reads('/retros/insights').length).toBe(2);
});

test('ActionItemsTable_WhenPullFails_ShouldToastTheError', async ({ mount, page }) => {
  await stubRetros(page);
  await stubWorkItems(page, 400);
  await mount(
    <>
      <RetroInsightsPanel project={insightsProject} />
      <ToastHost />
    </>,
    open,
  );
  await page.getByTestId(ids.retroActionPull).first().click();
  await expect(page.getByTestId(componentTestIds.toast)).toContainText('Could not move it');
});

test('ActionItemsTable_WhenThemeIsRecurring_ShouldMarkItsRowsAndExplainActionItems', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await stubWorkItems(page);
  const view = await mount(table(retroInsights), open);
  await expect(view).toContainText('is a work item type');
  const recurringIds = retroInsights.themes
    .filter((t) => t.status === 'recurring')
    .map((t) => t.theme.id);
  const expected = retroInsights.actionItems.filter(
    (r) => r.theme && recurringIds.includes(r.theme.id),
  ).length;
  await expect(view.locator('[data-recurring]')).toHaveCount(expected);
});

for (const theme of ['light', 'dark'] as const) {
  test(`ActionItemsTable_WhenRendered_ShouldMatchItsScreenshot_${theme}`, async ({
    mount,
    page,
  }) => {
    await stubRetros(page);
    await stubWorkItems(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    const view = await mount(table(retroInsights), open);
    await expect(page.getByTestId(ids.retroActionPull).first()).toBeVisible();
    await expect(view).toHaveScreenshot(`action-items.${theme}.png`, { animations: 'disabled' });
  });
}

test('ActionItemsTable_WhenModeIsWeek_ShouldCarryItToTheVerdictChip', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(table(insightsWeeks), open);
  const withImpact = insightsWeeks.actionItems.filter((row) => row.impact !== null).length;
  expect(withImpact).toBeGreaterThan(0);
  await expect(
    page.getByTestId(ids.retroActionRow).locator('span[data-verdict][data-mode="week"]'),
  ).toHaveCount(withImpact);
});

test('ActionItemsTable_WhenRendered_ShouldShowFixAndCovers', async ({ mount, page }) => {
  await stubRetros(page);
  await stubWorkItems(page);
  // The pinned fixture has no fix types (legacy rows), so give each row one here.
  const items = retroInsights.actionItems.map((r, i) => ({
    ...r,
    action: {
      ...r.action,
      fixType: retroFixOptions[i % retroFixOptions.length].id,
      pointCount: i,
    },
  }));
  await mount(table({ ...retroInsights, actionItems: items }), open);
  const rows = page.getByTestId(ids.retroActionRow);
  for (const [index] of items.entries()) {
    await expect(rows.nth(index).locator('[data-cell="fix"]')).toHaveText(
      retroFixOptions[index % retroFixOptions.length].label,
    );
    await expect(rows.nth(index).locator('[data-cell="covers"]')).toHaveText(String(index));
  }
});

test('ActionItemsTable_WhenFixTypeIsNull_ShouldShowADash', async ({ mount, page }) => {
  await stubRetros(page);
  await stubWorkItems(page);
  const legacy = {
    ...retroInsights,
    actionItems: retroInsights.actionItems.map((r) => ({
      ...r,
      action: { ...r.action, fixType: null },
    })),
  };
  await mount(table(legacy), open);
  await expect(
    page.getByTestId(ids.retroActionRow).first().locator('[data-cell="fix"]'),
  ).toHaveText('—');
});

for (const width of [1280, 1440]) {
  test(`ActionItemsTable_At${width}_ShouldNotScrollInsideItsColumn`, async ({ mount, page }) => {
    await stubRetros(page);
    await stubWorkItems(page);
    await page.setViewportSize({ width: width - 240, height: 800 });
    await mount(table(retroInsights), open);
    const wrap = page.getByTestId(ids.retroActionsTable).locator('..');
    await expect(page.getByTestId(ids.retroActionRow).first()).toBeVisible();
    const [scroll, client] = await wrap.evaluate((el) => [el.scrollWidth, el.clientWidth]);
    expect(scroll).toBeLessThanOrEqual(client);
  });
}

async function serveOffer(page: Page, dialog: RetroActionDialogDto) {
  const queries: string[] = [];
  await page.route(
    (url) => url.pathname.endsWith('/retros/action-dialog'),
    (route) => {
      queries.push(new URL(route.request().url()).search);
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(dialog),
      });
    },
  );
  return queries;
}

const offer = { ...actionDialog, selected: [] };

test('ActionItemsTable_WhenAddActionItemIsClicked_ShouldOpenTheDialogWithNothingTicked', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  const queries = await serveOffer(page, offer);
  await mount(table(retroInsights), open);
  await expect(page.getByTestId(retroSharedTestIds.retroActionDialog)).toHaveCount(0);
  await page.getByTestId(ids.retroActionsAdd).click();
  await expect(page.getByTestId(retroSharedTestIds.retroActionDialog)).toBeVisible();
  await expect(page.getByTestId(retroSharedTestIds.retroActionTitle)).toHaveValue('');
  await expect(page.getByTestId(retroSharedTestIds.retroActionTheme)).toHaveValue('');
  await expect(page.getByTestId(retroSharedTestIds.retroActionSave)).toBeDisabled();
  const checked = page.getByTestId(retroSharedTestIds.retroActionPick).getByRole('checkbox');
  await expect(checked.first()).not.toBeChecked();
  const query = new URLSearchParams(queries[0]);
  expect(query.getAll('point')).toEqual([]);
  expect(query.get('item')).toBeNull();
});

test('ActionItemsTable_WhenThereAreNoActionItems_ShouldStillShowAddActionItem', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(table({ ...retroInsights, actionItems: [] }), open);
  await expect(page.getByText('Nothing here.')).toBeVisible();
  await expect(page.getByTestId(ids.retroActionsAdd)).toBeVisible();
});

test('ActionItemsTable_WhenCreatedFromTheButton_ShouldPostOnePointAndRefetch', async ({
  mount,
  page,
}) => {
  const stub = await stubRetros(page);
  await serveOffer(page, offer);
  await mount(<RetroInsightsPanel project={insightsProject} />, open);
  await expect.poll(() => stub.reads('/retros/insights').length).toBe(1);
  await page.getByTestId(ids.retroActionsAdd).click();
  const save = page.getByTestId(retroSharedTestIds.retroActionSave);
  await expect(save).toBeDisabled();
  await page.getByTestId(retroSharedTestIds.retroActionTitle).fill('Name the test double');
  await expect(save).toBeDisabled();
  const first = page.getByTestId(retroSharedTestIds.retroActionPick).first();
  await first.getByRole('checkbox').check();
  await expect(save).toBeEnabled();
  await save.click();
  await expect(page.getByTestId(retroSharedTestIds.retroActionDialog)).toHaveCount(0);
  const writes = stub.writes();
  expect(writes).toHaveLength(1);
  const body = writes[0].body as { title: string; pointIds: string[] };
  expect(body.title).toBe('Name the test double');
  expect(body.pointIds).toEqual([offer.offered[0].id]);
  await expect.poll(() => stub.reads('/retros/insights').length).toBe(2);
});
