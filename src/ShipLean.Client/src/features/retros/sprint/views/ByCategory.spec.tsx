import { expect, test } from '@playwright/experimental-ct-react';
import type { ProjectDto } from '../../../../api/projects';
import type { RetroPointDto, SprintRetroDto } from '../../../../api/retros';
import type { SprintDto } from '../../../../api/sprints';
import { retroActionRef, sprintRetro, stubRetros } from '../../ct/retroFixtures';
import { ToastHost } from '../../../../components/ToastHost';
import { componentTestIds } from '../../../../components/testIds';
import { retroSharedTestIds as shared } from '../../../retroShared/testIds';
import { retrosTestIds as ids } from '../../testIds';
import { SprintRetroPanel } from '../SprintRetroPanel';
import { ByCategory } from './ByCategory';

const project = { id: 'p1', key: 'SHIP' } as ProjectDto;
const sprint = { id: 'sprint-24', name: 'Sprint 24' } as SprintDto;

const allPoints = sprintRetro.categories.flatMap((c) => c.groups.flatMap((g) => g.points));
const byRole = (agent: boolean) =>
  allPoints.filter((p: RetroPointDto) => (p.author.role !== 'person') === agent);

test('ByCategory_WhenOpened_ShouldRunColumnsInOrderWithGroupsLargestFirstAndUnthemedLast', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(<ByCategory project={project} sprint={sprint} retro={sprintRetro} />);

  const columns = page.getByTestId(ids.retroColumn);
  await expect(columns).toHaveCount(4);
  await expect(columns.nth(0)).toHaveAttribute('data-category', 'wentBadly');
  await expect(columns.nth(1)).toHaveAttribute('data-category', 'improve');
  await expect(columns.nth(2)).toHaveAttribute('data-category', 'notInPlan');
  await expect(columns.nth(3)).toHaveAttribute('data-category', 'wentWell');

  const improve = columns.nth(1).getByTestId(ids.retroThemeGroup);
  await expect(improve).toHaveCount(3);
  await expect(improve.last()).toHaveAttribute('data-theme', 'unthemed');
  const badly = columns.nth(0).getByTestId(ids.retroThemeGroup);
  await expect(badly.first().getByTestId(ids.retroPoint)).toHaveCount(2);

  await expect(badly.first()).toHaveAttribute('data-recurring', 'true');
  await expect(badly.first().getByTestId(shared.retroThemeChip)).toContainText('Recurring');
  await expect(badly.last()).toHaveAttribute('data-recurring', 'false');
  await expect(badly.last().getByTestId(shared.retroThemeChip)).not.toContainText('Recurring');
});

test('ByCategory_WhenFeaturePointShown_ShouldShowFeatureMarkerAndRootCause', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(<ByCategory project={project} sprint={sprint} retro={sprintRetro} />);

  const feature = page.locator(`[data-feature="true"][data-category="wentBadly"]`);
  await expect(feature).toHaveCount(1);
  await expect(feature).toContainText('Feature');
  await expect(feature).toContainText('Root cause');
});

test('ByCategory_WhenWrittenByChanged_ShouldHideTheOtherAuthorsPoints', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<ByCategory project={project} sprint={sprint} retro={sprintRetro} />);
  const filter = page.getByTestId(ids.retroWrittenByFilter);
  const rows = page.getByTestId(ids.retroPoint);
  await expect(rows).toHaveCount(allPoints.length);

  await filter.locator('[data-value="agents"]').click();
  await expect(rows).toHaveCount(byRole(true).length);
  await expect(
    page.getByTestId(shared.retroAuthorChip).and(page.locator('[data-role="person"]')),
  ).toHaveCount(0);

  await filter.locator('[data-value="people"]').click();
  await expect(rows).toHaveCount(byRole(false).length);
  await expect(
    page.getByTestId(shared.retroAuthorChip).and(page.locator('[data-role="subAgent"]')),
  ).toHaveCount(0);
  await expect(
    page.getByTestId(shared.retroAuthorChip).and(page.locator('[data-role="conductor"]')),
  ).toHaveCount(0);

  await filter.locator('[data-value="all"]').click();
  await expect(rows).toHaveCount(allPoints.length);
});

test('ByCategory_WhenThemeMoved_ShouldPutTheThemeIdOrTheNewName', async ({ mount, page }) => {
  const stub = await stubRetros(page);
  await mount(<ByCategory project={project} sprint={sprint} retro={sprintRetro} />);
  const point = page.locator(`[data-point-id="${allPoints[0].id}"]`);

  await point.getByTestId(ids.retroPointMoveTheme).click();
  const picker = page.getByTestId(shared.retroThemePicker);
  await picker.getByRole('combobox').fill('Merge');
  await picker.getByRole('option').first().click();
  await expect.poll(() => stub.writes().length).toBe(1);
  const first = stub.writes()[0];
  expect(first.method).toBe('PUT');
  expect(first.path).toBe(`/api/retros/points/${allPoints[0].id}/theme`);
  expect(first.body).toMatchObject({ themeName: null });
  expect((first.body as { themeId: string }).themeId).toMatch(/\S/);

  await point.getByTestId(ids.retroPointMoveTheme).click();
  await picker.getByRole('combobox').fill('Brand new theme');
  await page.getByTestId(shared.retroThemePickerAdd).click();
  await expect.poll(() => stub.writes().length).toBe(2);
  expect(stub.writes()[1].body).toEqual({ themeId: null, themeName: 'Brand new theme' });
});

test('ByCategory_WhenPointsListed_ShouldOfferActionItemOnImprovePointsWithoutOneOnly', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(<ByCategory project={project} sprint={sprint} retro={sprintRetro} />);
  const expected = allPoints.filter((p) => p.category === 'improve' && p.action === null);
  await expect(page.getByTestId(ids.retroPointAddAction)).toHaveCount(expected.length);
  for (const p of allPoints.filter((x) => x.category !== 'improve')) {
    await expect(
      page.locator(`[data-point-id="${p.id}"]`).getByTestId(ids.retroPointAddAction),
    ).toHaveCount(0);
  }
});

test('ByCategory_WhenActionItemAdded_ShouldPostAndShowTheReturnedKeyAfterRefetch', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  const target = allPoints.find((p) => p.category === 'improve' && p.action === null);
  if (!target) throw new Error('fixture has no improve point without an action item');
  let created = false;
  const withAction: SprintRetroDto = {
    ...sprintRetro,
    categories: sprintRetro.categories.map((c) => ({
      ...c,
      groups: c.groups.map((g) => ({
        ...g,
        points: g.points.map((p) => (p.id === target.id ? { ...p, action: retroActionRef } : p)),
      })),
    })),
  };
  // Registered after the stub, so it answers first: the retro read changes once the action exists.
  await page.route(
    (url) => /^\/api\/sprints\/[^/]+\/retro$/.test(url.pathname),
    (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(created ? withAction : sprintRetro),
      }),
  );
  await page.route(
    (url) => url.pathname === `/api/retros/points/${target.id}/action`,
    (route) => {
      created = true;
      return route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify(retroActionRef),
      });
    },
  );
  // The panel owns the retro query, so the refetch after the write reaches the view.
  await mount(<SprintRetroPanel project={project} sprint={sprint} />);
  const point = page.locator(`[data-point-id="${target.id}"]`);
  await point.getByTestId(ids.retroPointAddAction).click();

  await expect(point).toContainText(retroActionRef.key);
  await expect(point.getByTestId(componentTestIds.stateDot)).toContainText(
    retroActionRef.state.name,
  );
  await expect(point.getByTestId(ids.retroPointAddAction)).toHaveCount(0);
});

test('ByCategory_WhenPointsListed_ShouldOfferArchiveOnlyWhereCanArchive', async ({
  mount,
  page,
}) => {
  const stub = await stubRetros(page);
  await mount(<ByCategory project={project} sprint={sprint} retro={sprintRetro} />);
  await expect(page.getByTestId(ids.retroPointArchive)).toHaveCount(
    allPoints.filter((p) => p.canArchive).length,
  );
  for (const p of allPoints.filter((x) => !x.canArchive)) {
    await expect(
      page.locator(`[data-point-id="${p.id}"]`).getByTestId(ids.retroPointArchive),
    ).toHaveCount(0);
  }
  const mine = allPoints.find((p) => p.canArchive);
  if (!mine) throw new Error('fixture has no archivable point');
  await page.locator(`[data-point-id="${mine.id}"]`).getByTestId(ids.retroPointArchive).click();
  await expect.poll(() => stub.writes().length).toBe(1);
  expect(stub.writes()[0].path).toBe(`/api/retros/points/${mine.id}/archive`);
});

test('ByCategory_WhenAgentPointShown_ShouldOfferNoTextEditing', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<ByCategory project={project} sprint={sprint} retro={sprintRetro} />);
  const agent = page.locator(`[data-point-id="${byRole(true)[0].id}"]`);
  await expect(agent.getByTestId(ids.retroPointMoveTheme)).toHaveCount(1);
  await expect(agent.getByRole('textbox')).toHaveCount(0);
  await expect(page.getByTestId(ids.retroColumn).getByRole('textbox')).toHaveCount(0);
  await expect(agent.locator('[contenteditable]')).toHaveCount(0);
});

test('ByCategory_WhenActionItemRefused_ShouldShowTheErrorAsAToastAndRefetch', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  const target = allPoints.find((p) => p.category === 'improve' && p.action === null);
  if (!target) throw new Error('fixture has no improve point without an action item');
  let retroReads = 0;
  await page.route(
    (url) => /^\/api\/sprints\/[^/]+\/retro$/.test(url.pathname),
    (route) => {
      retroReads += 1;
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(sprintRetro),
      });
    },
  );
  await page.route(
    (url) => url.pathname === `/api/retros/points/${target.id}/action`,
    (route) =>
      route.fulfill({
        status: 409,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'This point already has an action item.', code: 'conflict' }),
      }),
  );
  await mount(
    <>
      <ToastHost />
      <SprintRetroPanel project={project} sprint={sprint} />
    </>,
  );
  const point = page.locator(`[data-point-id="${target.id}"]`);
  await expect(point).toBeVisible();
  const readsBefore = retroReads;
  await point.getByTestId(ids.retroPointAddAction).click();

  await expect(page.getByTestId(componentTestIds.toast)).toContainText(
    'This point already has an action item.',
  );
  await expect.poll(() => retroReads).toBeGreaterThan(readsBefore);
});

test('ByCategory_WhenMoveThemeOpen_ShouldCloseOnEscapeAndOnClickOutside', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(<ByCategory project={project} sprint={sprint} retro={sprintRetro} />);
  const point = page.locator(`[data-point-id="${allPoints[0].id}"]`);
  const picker = page.getByTestId(shared.retroThemePicker);

  await point.getByTestId(ids.retroPointMoveTheme).click();
  await expect(picker).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(picker).toHaveCount(0);

  await point.getByTestId(ids.retroPointMoveTheme).click();
  await expect(picker).toBeVisible();
  await page.getByTestId(ids.retroWrittenByFilter).click();
  await expect(picker).toHaveCount(0);
});

test('ByCategory_WhenSprintHasAFeatureRetro_ShouldShowTheCalloutAboveTheColumns', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  const card = sprintRetro.cards.find((c) => c.workItem?.type.id === 'feature');
  if (!card?.workItem) throw new Error('fixture has no feature retro');
  await mount(<ByCategory project={project} sprint={sprint} retro={sprintRetro} />);
  const callout = page.getByTestId(ids.retroFeatureCallout);
  await expect(callout).toContainText(`Feature retro:`);
  await expect(callout).toContainText(`${card.workItem.key} ${card.workItem.title}`);
  await expect(callout).toContainText(`Compiled by ${card.author.user.name}`);
  await expect(callout).toContainText(`from ${card.compiledFrom.length} story retros`);
});

test('ByCategory_WhenACategoryHasNoPoints_ShouldSayNothingHere', async ({ mount, page }) => {
  await stubRetros(page);
  const empty: SprintRetroDto = {
    ...sprintRetro,
    categories: sprintRetro.categories.map((c) =>
      c.category === 'wentWell' ? { ...c, groups: [] } : c,
    ),
  };
  await mount(<ByCategory project={project} sprint={sprint} retro={empty} />);
  const column = page.getByTestId(ids.retroColumn).and(page.locator('[data-category="wentWell"]'));
  await expect(column).toContainText('Nothing here.');
});

for (const theme of ['light', 'dark'] as const) {
  test(`ByCategory_WhenRendered_ShouldMatchItsScreenshot_${theme}`, async ({ mount, page }) => {
    await stubRetros(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    const view = await mount(<ByCategory project={project} sprint={sprint} retro={sprintRetro} />);
    await expect(page.getByTestId(ids.retroColumn)).toHaveCount(4);
    await expect(view).toHaveScreenshot(`by-category.${theme}.png`, { animations: 'disabled' });
  });
}

test('ByCategory_WhenSprintHasNoFeatureRetro_ShouldShowNoCallout', async ({ mount, page }) => {
  await stubRetros(page);
  const without: SprintRetroDto = {
    ...sprintRetro,
    cards: sprintRetro.cards.filter((c) => c.workItem?.type.id !== 'feature'),
  };
  await mount(<ByCategory project={project} sprint={sprint} retro={without} />);
  await expect(page.getByTestId(ids.retroColumn)).toHaveCount(4);
  await expect(page.getByTestId(ids.retroFeatureCallout)).toHaveCount(0);
});
