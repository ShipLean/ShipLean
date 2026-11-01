import { expect, test } from '@playwright/experimental-ct-react';
import type { ProjectDto } from '../../../../api/projects';
import type { SprintDto } from '../../../../api/sprints';
import { sprintRetro } from '../../ct/retroFixtures';
import { componentTestIds } from '../../../../components/testIds';
import { retroSharedTestIds as shared } from '../../../retroShared/testIds';
import { retrosTestIds as ids } from '../../testIds';
import { ByWorkItem } from './ByWorkItem';

const project = { id: 'p1', key: 'SHIP' } as ProjectDto;
const sprint = { id: 'sprint-24', name: 'Sprint 24' } as SprintDto;
const mountOptions = { hooksConfig: { route: '/p/SHIP/boards/stories' } };
const element = <ByWorkItem project={project} sprint={sprint} retro={sprintRetro} />;

test('ByWorkItem_WhenOpened_ShouldShowOneCardPerRetroWithFeatureCardsFirst', async ({
  mount,
  page,
}) => {
  await mount(element, mountOptions);
  const cards = page.getByTestId(ids.retroCardList).getByTestId(shared.retroCard);
  await expect(cards).toHaveCount(sprintRetro.cards.length);
  await expect(cards.first()).toHaveAttribute('data-feature', 'true');
  await expect(page.locator('[data-feature="true"]')).toHaveCount(1);
});

test('ByWorkItem_WhenTheFeatureCardComesLast_ShouldStillShowItFirst', async ({ mount, page }) => {
  const featureLast = {
    ...sprintRetro,
    cards: [
      ...sprintRetro.cards.filter((c) => c.workItem?.type.id !== 'feature'),
      ...sprintRetro.cards.filter((c) => c.workItem?.type.id === 'feature'),
    ],
  };
  await mount(<ByWorkItem project={project} sprint={sprint} retro={featureLast} />, mountOptions);
  const cards = page.getByTestId(shared.retroCard);
  await expect(cards.first()).toHaveAttribute('data-feature', 'true');
  await expect(cards.last()).toHaveAttribute('data-feature', 'false');
});

test('ByWorkItem_WhenOpened_ShouldNotShowActionChips', async ({ mount, page }) => {
  await mount(element, mountOptions);
  await expect(page.getByTestId(shared.retroCard).first()).toBeVisible();
  expect(sprintRetro.cards.some((c) => c.points.some((pt) => pt.action !== null))).toBe(true);
  await expect(page.locator('li[data-point-id] a[data-item-key]')).toHaveCount(0);
});

test('ByWorkItem_WhenOpened_ShouldStartWithTheFailureLogCollapsed', async ({ mount, page }) => {
  await mount(element, mountOptions);
  await expect(page.getByTestId(shared.retroCardLogToggle).first()).toHaveAttribute(
    'aria-expanded',
    'false',
  );
  await expect(page.getByTestId(shared.retroCardLog)).toHaveCount(0);
});

test('ByWorkItem_WhenARetroIsOnTheSprint_ShouldShowItAsSprint', async ({ mount, page }) => {
  await mount(element, mountOptions);
  const sprintCard = page.getByTestId(shared.retroCard).last();
  await expect(sprintCard.getByText('Sprint', { exact: true })).toBeVisible();
  await expect(sprintCard).toContainText('Sprint 24');
  await expect(sprintCard.getByTestId(componentTestIds.typeIcon)).toHaveCount(0);
});

test('ByWorkItem_WhenARetroIsCompiled_ShouldLinkTheCompiledFromKeys', async ({ mount, page }) => {
  await mount(element, mountOptions);
  const card = page.getByTestId(shared.retroCard).first();
  await expect(card).toContainText('Compiled from');
  const link = card.locator('a[data-item-key="SHIP-141"]');
  await expect(link).toHaveAttribute('href', '/p/SHIP/items/SHIP-141');
});

test('ByWorkItem_WhenWrittenByIsPeople_ShouldKeepOnlyPersonRetros', async ({ mount, page }) => {
  await mount(element, mountOptions);
  await page.getByTestId(ids.retroWrittenByFilter).locator('[data-value="people"]').click();
  const people = sprintRetro.cards.filter((c) => c.author.role === 'person' && c.points.length > 0);
  await expect(page.getByTestId(shared.retroCard)).toHaveCount(people.length);
});

for (const theme of ['light', 'dark'] as const) {
  test(`ByWorkItem_WhenRendered_ShouldMatchItsScreenshot_${theme}`, async ({ mount, page }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    const view = await mount(element, mountOptions);
    await expect(view).toHaveScreenshot(`by-work-item.${theme}.png`, { animations: 'disabled' });
  });
}
