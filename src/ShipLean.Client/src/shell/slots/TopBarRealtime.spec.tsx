import { expect, test } from '@playwright/experimental-ct-react';
import { meDto, projectSummary, stubJson } from '../../features/auth/authFixtures';
import { realtimeTestIds as ids } from '../../features/realtime/testIds';
import { member } from '../../realtime/testing/presenceFixtures';
import { TopBarRealtime } from './TopBarRealtime';

test('TopBarRealtime_WhenInstallLevel_ShouldRenderNothing', async ({ mount, page }) => {
  await stubJson(page, '/api/me', meDto());
  const component = await mount(<TopBarRealtime />, { hooksConfig: { route: '/profile' } });
  await expect(component).toBeEmpty();
  await expect(page.getByTestId(ids.liveDot)).toHaveCount(0);
});

test('TopBarRealtime_WhenProjectRoute_ShouldShowTheLiveDot', async ({ mount, page }) => {
  await stubJson(page, '/api/me', meDto());
  await stubJson(page, '/api/projects/by-key/SHIP', {
    ...projectSummary('SHIP'),
    id: 'p1',
  });
  await stubJson(page, '/api/projects/p1/members', [member(1)]);
  await mount(<TopBarRealtime />, { hooksConfig: { route: '/p/SHIP/sprints' } });
  // No hub in a component test: the dot still renders, not live.
  await expect(page.getByTestId(ids.liveDot)).toBeVisible();
  await expect(page.getByTestId(ids.liveDot)).not.toHaveAttribute('data-state', 'live');
});
