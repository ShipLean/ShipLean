import { expect, test } from '@playwright/experimental-ct-react';
import type { ProjectDto } from '../../../../api/projects';
import type { SprintDto } from '../../../../api/sprints';
import { ByWorkItemHarness } from '../../ct/ByWorkItemHarness';
import { sprintRetro, stubRetros } from '../../ct/retroFixtures';
import { componentTestIds } from '../../../../components/testIds';
import { retroSharedTestIds as shared } from '../../../retroShared/testIds';
import { retrosTestIds as ids } from '../../testIds';

const project = { id: 'p1', key: 'SHIP' } as ProjectDto;
const sprint = { id: 'sprint-24', name: 'Sprint 24' } as SprintDto;
const mountOptions = { hooksConfig: { route: '/p/SHIP/boards/stories' } };
const element = <ByWorkItemHarness project={project} sprint={sprint} retro={sprintRetro} />;

test('ByWorkItem_WhenOpened_ShouldShowOneBoardPerItemFeatureFirst', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(element, mountOptions);
  const cards = page.getByTestId(ids.retroCardList).getByTestId(shared.retroCard);
  await expect(cards).toHaveCount(sprintRetro.cards.length);
  await expect(cards.first()).toHaveAttribute('data-feature', 'true');
  await expect(page.locator('[data-feature="true"]')).toHaveCount(1);
  await expect(cards.first().getByTestId(shared.retroBoard)).toBeVisible();
  await expect(cards.first().getByTestId(shared.retroBoardColumn)).toHaveCount(4);
  for (const card of sprintRetro.cards) {
    const own = cards.filter({ has: page.locator(`[data-point-id="${card.points[0]?.id}"]`) });
    await expect(own.getByTestId(shared.retroNote)).toHaveCount(card.points.length);
  }
});

test('ByWorkItem_WhenTheFeatureCardComesLast_ShouldStillShowItFirst', async ({ mount, page }) => {
  await stubRetros(page);
  const featureLast = {
    ...sprintRetro,
    cards: [
      ...sprintRetro.cards.filter((c) => c.workItem?.type.id !== 'feature'),
      ...sprintRetro.cards.filter((c) => c.workItem?.type.id === 'feature'),
    ],
  };
  await mount(
    <ByWorkItemHarness project={project} sprint={sprint} retro={featureLast} />,
    mountOptions,
  );
  const cards = page.getByTestId(shared.retroCard);
  await expect(cards.first()).toHaveAttribute('data-feature', 'true');
  await expect(cards.last()).toHaveAttribute('data-feature', 'false');
});

test('ByWorkItem_WhenOpened_ShouldGiveNotesTheSameButtonsAsByCategory', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(element, mountOptions);
  const points = sprintRetro.cards.flatMap((c) => c.points);
  const undecided = points.filter((p) => p.decision === 'undecided');
  expect(undecided.length).toBeGreaterThan(0);
  await expect(page.getByTestId(shared.retroNote)).toHaveCount(points.length);
  await expect(page.getByTestId(shared.retroNoteAddAction)).toHaveCount(undecided.length);
  await expect(page.getByTestId(shared.retroNoteNotNeeded)).toHaveCount(undecided.length);
  await expect(page.getByTestId(shared.retroBoardAdd)).toHaveCount(4 * sprintRetro.cards.length);
});

test('ByWorkItem_WhenAnItemBoardHasANoteTheme_ShouldMoveItToAnotherTheme', async ({
  mount,
  page,
}) => {
  const stub = await stubRetros(page);
  await mount(element, mountOptions);
  const first = page.getByTestId(shared.retroCard).first().getByTestId(shared.retroNote).first();
  await expect(first.getByTestId(shared.retroNoteTheme)).toBeEnabled();
  await first.getByTestId(shared.retroNoteTheme).click();
  await page.getByRole('option').first().click();
  await expect.poll(() => stub.writes().map((w) => w.path.split('/').pop())).toContain('theme');
});

test('ByWorkItem_WhenAddedUnderAnItemsColumn_ShouldTargetThatItem', async ({ mount, page }) => {
  const stub = await stubRetros(page);
  await mount(element, mountOptions);
  const card = sprintRetro.cards.find((c) => c.workItem && c.workItem.type.id !== 'feature')!;
  const board = page.locator(`[data-item-key="${card.workItem!.key}"]`);
  await board.getByTestId(shared.retroBoardAdd).first().click();
  await page.getByTestId(shared.retroBoardAddText).fill('A new point');
  await page.getByTestId(shared.retroBoardAddSave).click();
  await expect
    .poll(() => stub.writes().find((w) => w.path.endsWith('/retros/points'))?.body)
    .toMatchObject({ workItemId: card.workItem!.id });
});

test('ByWorkItem_WhenALogIsReported_ShouldKeepItCollapsedBelowItsBoard', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(element, mountOptions);
  const card = page.locator('[data-item-key="SHIP-141"]');
  const toggle = card.getByTestId(shared.retroCardLogToggle);
  await expect(toggle).toHaveAttribute('aria-expanded', 'false');
  await expect(card.getByTestId(shared.retroCardLog)).toHaveCount(0);
  await toggle.click();
  await expect(card.getByTestId(shared.retroCardLog)).toBeVisible();
  const board = await card.getByTestId(shared.retroBoard).boundingBox();
  const log = await toggle.boundingBox();
  expect(log!.y).toBeGreaterThan(board!.y + board!.height - 1);
});

test('ByWorkItem_WhenToDecideChosen_ShouldFilterEachBoardKeepingItsCounts', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(
    <ByWorkItemHarness project={project} sprint={sprint} retro={sprintRetro} decide />,
    mountOptions,
  );
  const points = sprintRetro.cards.flatMap((c) => c.points);
  const undecided = points.filter((p) => p.decision === 'undecided');
  expect(undecided.length).toBeLessThan(points.length);
  await expect(page.getByTestId(shared.retroNote)).toHaveCount(undecided.length);
  for (const card of sprintRetro.cards) {
    const key = card.workItem?.key ?? 'sprint';
    const board = page.locator(`[data-item-key="${key}"]`);
    await expect(board.locator('[data-to-decide]')).toHaveCount(
      new Set(card.points.filter((p) => p.decision === 'undecided').map((p) => p.category)).size,
    );
  }
});

test('ByWorkItem_WhenARetroIsOnTheSprint_ShouldShowItAsSprint', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(element, mountOptions);
  const sprintCard = page.getByTestId(shared.retroCard).last();
  await expect(sprintCard.getByText('Sprint', { exact: true })).toBeVisible();
  await expect(sprintCard).toContainText('Sprint 24');
  await expect(sprintCard.getByTestId(componentTestIds.typeIcon)).toHaveCount(0);
});

test('ByWorkItem_WhenARetroIsCompiled_ShouldLinkTheCompiledFromKeys', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(element, mountOptions);
  const card = page.getByTestId(shared.retroCard).first();
  await expect(card).toContainText('Compiled from');
  const link = card.locator('a[data-item-key="SHIP-141"]');
  await expect(link).toHaveAttribute('href', '/p/SHIP/items/SHIP-141');
});

test('ByWorkItem_WhenWrittenByIsPeople_ShouldKeepOnlyPersonRetros', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(element, mountOptions);
  await page.getByTestId(ids.retroWrittenByFilter).locator('[data-value="people"]').click();
  const people = sprintRetro.cards.filter((c) => c.author.role === 'person' && c.points.length > 0);
  await expect(page.getByTestId(shared.retroCard)).toHaveCount(people.length);
});

for (const theme of ['light', 'dark'] as const) {
  test(`ByWorkItem_WhenRendered_ShouldMatchItsScreenshot_${theme}`, async ({ mount, page }) => {
    await stubRetros(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    const view = await mount(element, mountOptions);
    await expect(view).toHaveScreenshot(`by-work-item.${theme}.png`, { animations: 'disabled' });
  });
}
