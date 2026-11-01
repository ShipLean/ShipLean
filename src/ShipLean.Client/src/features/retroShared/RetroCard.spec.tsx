import { expect, test } from '@playwright/experimental-ct-react';
import { componentTestIds } from '../../components/testIds';
import { featureCard, sprintCard, subAgentCard } from './fixtures';
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
  const headings = await log.locator('dt').allTextContents();
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

for (const theme of ['light', 'dark'] as const) {
  test(`RetroCard_WhenRendered_ShouldMatchItsScreenshot_${theme}`, async ({ mount, page }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    const card = await mount(<RetroCard card={subAgentCard} />);
    await expect(card).toHaveScreenshot(`retro-card.${theme}.png`, { animations: 'disabled' });
  });
}
