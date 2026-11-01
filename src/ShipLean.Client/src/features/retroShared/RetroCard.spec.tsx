import { expect, test } from '@playwright/experimental-ct-react';
import { componentTestIds } from '../../components/testIds';
import { CardWidthHarness } from './ct/CardWidthHarness';
import { featureCard, sprintCard, subAgentCard } from './ct/fixtures';
import { RetroCard } from './RetroCard';
import { retroSharedTestIds as ids } from './testIds';

test('RetroCard_WhenFailureLogPresent_ShouldStartCollapsedAndExpandInOrder', async ({
  mount,
  page,
}) => {
  await mount(<RetroCard card={subAgentCard} />);
  await expect(page.getByTestId(ids.retroCardLog)).toHaveCount(0);

  await page.getByTestId(ids.retroCardLogToggle).click();
  const log = page.getByTestId(ids.retroCardLog);
  await expect(log).toBeVisible();
  const headings = await log.locator('[data-log-section]').allTextContents();
  expect(headings).toEqual(['Blockers', 'Assumptions disproved', 'Friction']);
  await expect(log).toContainText(subAgentCard.failureLog.blockers[0]);
});

test('RetroCard_WhenFeatureRetro_ShouldShowFeatureMarkerAndCompiledFromKeys', async ({
  mount,
  page,
}) => {
  await mount(<RetroCard card={featureCard} />);
  const card = page.getByTestId(ids.retroCard);
  await expect(card).toHaveAttribute('data-feature', 'true');
  await expect(card.getByText('Feature', { exact: true })).toBeVisible();
  for (const item of featureCard.compiledFrom) await expect(card).toContainText(item.key);
  await expect(page.getByTestId(ids.retroCardLogToggle)).toHaveCount(0);
});

test('RetroCard_WhenSprintLevelPersonRetro_ShouldShowSprintAndNoItem', async ({ mount, page }) => {
  await mount(<RetroCard card={sprintCard} />);
  const card = page.getByTestId(ids.retroCard);
  await expect(card.getByText('Sprint', { exact: true })).toBeVisible();
  await expect(card.getByTestId(componentTestIds.typeIcon)).toHaveCount(0);
});

test('RetroCard_WhenContainerIsNarrow_ShouldStackCategoriesInOneColumn', async ({
  mount,
  page,
}) => {
  await mount(<CardWidthHarness card={subAgentCard} width={380} />);
  const boxes = await page
    .getByTestId(ids.retroCard)
    .locator('section[data-category]')
    .evaluateAll((els) => els.map((el) => el.getBoundingClientRect().x));
  expect(boxes).toHaveLength(4);
  expect(new Set(boxes).size).toBe(1);
});

test('RetroCard_WhenContainerIsWide_ShouldLayCategoriesInOneRow', async ({ mount, page }) => {
  await mount(<CardWidthHarness card={subAgentCard} width={1000} />);
  const tops = await page
    .getByTestId(ids.retroCard)
    .locator('section[data-category]')
    .evaluateAll((els) => els.map((el) => el.getBoundingClientRect().y));
  expect(new Set(tops).size).toBe(1);
});

test('RetroCard_WhenHighlightPointIdGiven_ShouldHighlightOnlyThatPoint', async ({
  mount,
  page,
}) => {
  const target = subAgentCard.points[1];
  await mount(<RetroCard card={subAgentCard} highlightPointId={target.id} />);
  const card = page.getByTestId(ids.retroCard);
  await expect(card.locator('[data-highlighted="true"]')).toHaveCount(1);
  await expect(card.locator(`[data-point-id="${target.id}"]`)).toHaveAttribute(
    'data-highlighted',
    'true',
  );
});

test('RetroCard_WhenLogDefaultOpen_ShouldShowTheLogWithoutAClick', async ({ mount, page }) => {
  await mount(<RetroCard card={subAgentCard} logDefaultOpen />);
  await expect(page.getByTestId(ids.retroCardLog)).toBeVisible();
  await expect(page.getByTestId(ids.retroCardLogToggle)).toHaveAttribute('aria-expanded', 'true');
});

test('RetroCard_WhenHideEmptyCategories_ShouldLeaveOutCategoriesWithNoPoints', async ({
  mount,
  page,
}) => {
  await mount(<RetroCard card={featureCard} hideEmptyCategories />);
  const sections = page.getByTestId(ids.retroCard).locator('section[data-category]');
  await expect(sections).toHaveCount(3);
  await expect(page.getByTestId(ids.retroCard).locator('[data-category="notInPlan"]')).toHaveCount(
    0,
  );
});

test('RetroCard_WhenCategoryEmpty_ShouldShowAnEmDash', async ({ mount, page }) => {
  await mount(<RetroCard card={featureCard} />);
  await expect(
    page.getByTestId(ids.retroCard).locator('section[data-category="notInPlan"]'),
  ).toContainText('—');
});

test('RetroCard_WhenPointHasAction_ShouldLinkItsKeyAndStateToTheWorkItem', async ({
  mount,
  page,
}) => {
  const action = featureCard.points.find((p) => p.action)!.action!;
  await mount(<RetroCard card={featureCard} />, {
    hooksConfig: { route: '/p/SHIP/boards/stories' },
  });
  const chip = page.getByTestId(ids.retroCard).locator(`[data-item-key="${action.key}"]`);
  await expect(chip).toHaveAttribute('href', `/p/SHIP/items/${action.key}`);
  await expect(chip).toContainText(action.state.name);
});

test('RetroCard_WhenPointHasTheme_ShouldShowItAsAHashLineWithoutRecurringChip', async ({
  mount,
  page,
}) => {
  const point = featureCard.points.find((p) => p.theme)!;
  await mount(<RetroCard card={{ ...featureCard, points: [{ ...point, recurring: true }] }} />);
  const line = page.getByTestId(ids.retroThemeChip);
  await expect(line).toHaveText(`# ${point.theme!.name}`);
  await expect(page.getByTestId(ids.retroCard)).not.toContainText('Recurring');
});

test('RetroCard_WhenCompiledFrom_ShouldSeparateKeysWithCommasAndEndWithAPeriod', async ({
  mount,
  page,
}) => {
  await mount(<RetroCard card={featureCard} />);
  const keys = featureCard.compiledFrom.map((i) => i.key).join(', ');
  await expect(page.getByTestId(ids.retroCard)).toContainText(`Compiled from ${keys}.`);
});

for (const theme of ['light', 'dark'] as const) {
  test(`RetroCard_WhenRendered_ShouldMatchItsScreenshot_${theme}`, async ({ mount, page }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    const card = await mount(<RetroCard card={subAgentCard} />);
    await expect(card).toHaveScreenshot(`retro-card.${theme}.png`, { animations: 'disabled' });
  });
}
