import { expect, test } from '@playwright/experimental-ct-react';
import type { ProjectDto } from '@shiplean/core/api/projects';
import type { SprintDto } from '@shiplean/core/api/sprints';
import { sprintRetro } from '../ct/retroFixtures';
import { retrosTestIds as ids } from '../testIds';
import { SprintRetroHeader } from './SprintRetroHeader';

const project = { id: 'p1', key: 'SHIP' } as ProjectDto;
const sprint = { id: 'sprint-24', name: 'Sprint 24' } as SprintDto;
const props = { project, sprint, retro: sprintRetro };

test('SprintRetroHeader_WhenRendered_ShouldShowTheKpisFromTheFixture', async ({ mount, page }) => {
  await mount(<SprintRetroHeader {...props} />, {
    hooksConfig: { route: '/p/SHIP/boards/stories' },
  });
  const { kpis } = sprintRetro;
  await expect(page.getByTestId(ids.retroKpiWithRetro)).toHaveText(
    `${kpis.withRetro} / ${kpis.finished}`,
  );
  await expect(page.getByTestId(ids.retroKpiRecurring)).toHaveText(String(kpis.recurringThemes));
  await expect(page.getByTestId(ids.retroKpiActions)).toHaveText(`${kpis.actionItemsRaised} new`);
  await expect(
    page.getByTestId(ids.retroHeader).getByRole('link', { name: 'insights' }),
  ).toHaveAttribute('href', '/p/SHIP/retros/insights');
});

test('SprintRetroHeader_WhenRendered_ShouldShowPointsToDecide', async ({ mount, page }) => {
  await mount(
    <SprintRetroHeader
      {...props}
      retro={{ ...sprintRetro, kpis: { ...sprintRetro.kpis, pointsToDecide: 9 } }}
    />,
    {
      hooksConfig: { route: '/p/SHIP/boards/stories' },
    },
  );
  await expect(page.getByTestId(ids.retroKpiToDecide)).toHaveText('9');
  await expect(page.getByTestId(ids.retroHeader)).toContainText('Points to decide');
});

for (const theme of ['light', 'dark'] as const) {
  test(`SprintRetroHeader_WhenRendered_ShouldMatchItsScreenshot_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    const header = await mount(<SprintRetroHeader {...props} />, {
      hooksConfig: { route: '/p/SHIP/boards/stories' },
    });
    await expect(header).toHaveScreenshot(`sprint-retro-header.${theme}.png`, {
      animations: 'disabled',
    });
  });
}
