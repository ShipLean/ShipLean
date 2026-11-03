import { expect, test } from '@playwright/experimental-ct-react';
import type { ProjectDto } from '../../../../api/projects';
import type { RetroPointDto, SprintRetroDto } from '../../../../api/retros';
import type { SprintDto } from '../../../../api/sprints';
import { SprintRetroAddressHarness } from '../../ct/SprintRetroAddressHarness';
import { ByCategoryHarness } from '../../ct/ByCategoryHarness';
import { sprintRetro, stubRetros } from '../../ct/retroFixtures';
import { retroSharedTestIds as shared } from '../../../retroShared/testIds';
import { retrosTestIds as ids } from '../../testIds';
import { SprintRetroPanel } from '../SprintRetroPanel';

const project = { id: 'p1', key: 'SHIP' } as ProjectDto;
const sprint = { id: 'sprint-24', name: 'Sprint 24' } as SprintDto;

const retroRoute = { hooksConfig: { route: '/p/SHIP/sprints/sprint-24/retro' } };

const allPoints = sprintRetro.categories.flatMap((c) => c.groups.flatMap((g) => g.points));
const byRole = (agent: boolean) =>
  allPoints.filter((p: RetroPointDto) => (p.author.role !== 'person') === agent);

test('ByCategory_WhenOpened_ShouldRunColumnsWentWellToCanBeImproved', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<ByCategoryHarness project={project} sprint={sprint} retro={sprintRetro} />);

  const columns = page.getByTestId(shared.retroBoardColumn);
  await expect(columns).toHaveCount(4);
  await expect(columns.nth(0)).toHaveAttribute('data-category', 'wentWell');
  await expect(columns.nth(1)).toHaveAttribute('data-category', 'notInPlan');
  await expect(columns.nth(2)).toHaveAttribute('data-category', 'wentBadly');
  await expect(columns.nth(3)).toHaveAttribute('data-category', 'improve');
  await expect(page.getByTestId(shared.retroBoard)).toBeVisible();
});

test('ByCategory_WhenOpened_ShouldShowNoToolbarAddAPoint', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<SprintRetroPanel project={project} sprint={sprint} />, retroRoute);
  await expect(page.getByTestId(shared.retroBoard)).toBeVisible();
  await expect(page.getByTestId(ids.retroAddPoint)).toHaveCount(0);
  await expect(page.getByTestId(shared.retroBoardAdd)).toHaveCount(4);
});

test('ByCategory_WhenToDecideChosen_ShouldWriteTheAddressAndHideDecidedNotesKeepingCounts', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(<SprintRetroAddressHarness project={project} sprint={sprint} />, retroRoute);
  const notes = page.getByTestId(shared.retroNote);
  await expect(notes).toHaveCount(allPoints.length);
  const filter = page.getByTestId(ids.retroToDecideFilter);

  await filter.locator('[data-value="open"]').click();
  await expect(page.getByTestId('test-address')).toHaveText('?decide=open');
  const undecided = allPoints.filter((p) => p.decision === 'undecided');
  await expect(notes).toHaveCount(undecided.length);
  expect(undecided.length).toBeLessThan(allPoints.length);
  for (const column of sprintRetro.categories) {
    const header = page
      .getByTestId(shared.retroBoardColumn)
      .and(page.locator(`[data-category="${column.category}"]`));
    if (column.toDecide > 0) {
      await expect(header.locator('[data-to-decide]')).toHaveAttribute(
        'data-to-decide',
        String(column.toDecide),
      );
    }
  }

  await filter.locator('[data-value="all"]').click();
  await expect(page.getByTestId('test-address')).toHaveText('');
  await expect(notes).toHaveCount(allPoints.length);
});

test('ByCategory_WhenToDecideInTheAddress_ShouldOpenFiltered', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<SprintRetroAddressHarness project={project} sprint={sprint} />, {
    hooksConfig: { route: '/p/SHIP/sprints/sprint-24/retro?decide=open' },
  });
  await expect(page.getByTestId(shared.retroNote)).toHaveCount(
    allPoints.filter((p) => p.decision === 'undecided').length,
  );
  await expect(
    page.getByTestId(ids.retroToDecideFilter).locator('[data-value="open"]'),
  ).toHaveAttribute('aria-pressed', 'true');
});

test('ByCategory_WhenPointInTheAddress_ShouldHighlightThatNote', async ({ mount, page }) => {
  await stubRetros(page);
  const target = allPoints[0];
  await mount(
    <ByCategoryHarness project={project} sprint={sprint} retro={sprintRetro} point={target.id} />,
  );
  const highlighted = page.locator('[data-highlighted="true"]');
  await expect(highlighted).toHaveCount(1);
  await expect(highlighted).toHaveAttribute('data-point-id', target.id);
});

test('ByCategory_WhenFeaturePointShown_ShouldShowTheRootCause', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<ByCategoryHarness project={project} sprint={sprint} retro={sprintRetro} />);

  const withCause = page.getByTestId(shared.retroNote).filter({ hasText: 'Root cause' });
  await expect(withCause).toHaveCount(allPoints.filter((p) => p.rootCause).length);
});

test('ByCategory_WhenWrittenByChanged_ShouldHideTheOtherAuthorsPoints', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<ByCategoryHarness project={project} sprint={sprint} retro={sprintRetro} />);
  const filter = page.getByTestId(ids.retroWrittenByFilter);
  const rows = page.getByTestId(shared.retroNote);
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

test('ByCategory_WhenPointsListed_ShouldShowActionItemAsSolidOnImproveAndLinkElsewhere', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(<ByCategoryHarness project={project} sprint={sprint} retro={sprintRetro} />);
  const undecided = allPoints.filter((p) => p.decision === 'undecided');
  await expect(page.getByTestId(shared.retroNoteAddAction)).toHaveCount(undecided.length);
  for (const p of undecided) {
    await expect(
      page.locator(`[data-point-id="${p.id}"]`).getByTestId(shared.retroNoteAddAction),
    ).toHaveAttribute('data-variant', p.category === 'improve' ? 'button' : 'link');
  }
  for (const p of allPoints.filter((x) => x.decision !== 'undecided')) {
    await expect(
      page.locator(`[data-point-id="${p.id}"]`).getByTestId(shared.retroNoteAddAction),
    ).toHaveCount(0);
  }
});

test('ByCategory_WhenActionItemPressed_ShouldOpenTheActionDialogForThatPoint', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  const target = allPoints.find((p) => p.decision === 'undecided');
  if (!target) throw new Error('fixture has no undecided point');
  await mount(<SprintRetroPanel project={project} sprint={sprint} />, retroRoute);
  await page
    .locator(`[data-point-id="${target.id}"]`)
    .getByTestId(shared.retroNoteAddAction)
    .click();
  await expect(page.getByTestId(shared.retroActionDialog)).toBeVisible();
});

test('ByCategory_WhenPointsListed_ShouldOfferMoreOnlyWhereCanArchiveAndArchiveFromIt', async ({
  mount,
  page,
}) => {
  const stub = await stubRetros(page);
  await mount(<ByCategoryHarness project={project} sprint={sprint} retro={sprintRetro} />);
  await expect(page.getByTestId(shared.retroNoteMore)).toHaveCount(
    allPoints.filter((p) => p.canArchive).length,
  );
  const mine = allPoints.find((p) => p.canArchive);
  if (!mine) throw new Error('fixture has no archivable point');
  const note = page.locator(`[data-point-id="${mine.id}"]`);
  await note.getByTestId(shared.retroNoteMore).click();
  await note.getByTestId(shared.retroNoteArchive).click();
  await expect.poll(() => stub.writes().length).toBe(1);
  expect(stub.writes()[0].path).toBe(`/api/retros/points/${mine.id}/archive`);
});

test('ByCategory_WhenAgentPointShown_ShouldOfferNoTextEditing', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<ByCategoryHarness project={project} sprint={sprint} retro={sprintRetro} />);
  const agent = page.locator(`[data-point-id="${byRole(true)[0].id}"]`);
  await expect(agent).toHaveCount(1);
  await expect(agent.getByRole('textbox')).toHaveCount(0);
  await expect(page.getByTestId(shared.retroBoardColumn).getByRole('textbox')).toHaveCount(0);
  await expect(agent.locator('[contenteditable]')).toHaveCount(0);
});

test('ByCategory_WhenSprintHasAFeatureRetro_ShouldShowTheCalloutAboveTheColumns', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  const card = sprintRetro.cards.find((c) => c.workItem?.type.id === 'feature');
  if (!card?.workItem) throw new Error('fixture has no feature retro');
  await mount(<ByCategoryHarness project={project} sprint={sprint} retro={sprintRetro} />);
  const callout = page.getByTestId(ids.retroFeatureCallout);
  await expect(callout).toContainText(`Feature retro:`);
  await expect(callout).toContainText(`${card.workItem.key} ${card.workItem.title}`);
  await expect(callout).toContainText(`Compiled by ${card.author.user.name}`);
  await expect(callout).toContainText(`from ${card.compiledFrom.length} story retros`);
});

test('ByCategory_WhenACategoryHasNoPoints_ShouldShowNoNotesInIt', async ({ mount, page }) => {
  await stubRetros(page);
  const empty: SprintRetroDto = {
    ...sprintRetro,
    categories: sprintRetro.categories.map((c) =>
      c.category === 'wentWell' ? { ...c, groups: [] } : c,
    ),
  };
  await mount(<ByCategoryHarness project={project} sprint={sprint} retro={empty} />);
  const column = page
    .getByTestId(shared.retroBoardColumn)
    .and(page.locator('[data-category="wentWell"]'));
  await expect(column.getByTestId(shared.retroNote)).toHaveCount(0);
  await expect(column.locator('[data-count="shown"]')).toHaveText('0');
});

for (const theme of ['light', 'dark'] as const) {
  test(`ByCategory_WhenRendered_ShouldMatchItsScreenshot_${theme}`, async ({ mount, page }) => {
    await stubRetros(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    const view = await mount(
      <ByCategoryHarness project={project} sprint={sprint} retro={sprintRetro} />,
    );
    await expect(page.getByTestId(shared.retroBoardColumn)).toHaveCount(4);
    await expect(view).toHaveScreenshot(`by-category.${theme}.png`, { animations: 'disabled' });
  });
}

test('ByCategory_WhenSprintHasNoFeatureRetro_ShouldShowNoCallout', async ({ mount, page }) => {
  await stubRetros(page);
  const without: SprintRetroDto = {
    ...sprintRetro,
    cards: sprintRetro.cards.filter((c) => c.workItem?.type.id !== 'feature'),
  };
  await mount(<ByCategoryHarness project={project} sprint={sprint} retro={without} />);
  await expect(page.getByTestId(shared.retroBoardColumn)).toHaveCount(4);
  await expect(page.getByTestId(ids.retroFeatureCallout)).toHaveCount(0);
});
