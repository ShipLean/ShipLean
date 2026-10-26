import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../AppRoutes';
import { appTestIds } from '../../testIds';
import { meDto, projectSummary, stubInstance, stubJson } from './authFixtures';
import { authTestIds as ids } from './testIds';

test('RootRedirect_WhenSignedInWithProjects_ShouldOpenTheFirstProject', async ({ mount, page }) => {
  await stubJson(page, '/api/me', meDto());
  await stubJson(page, '/api/projects', [projectSummary('OPS'), projectSummary('SHIP')]);
  await mount(<AppRoutes />, { hooksConfig: { route: '/' } });
  await expect(page.getByTestId(appTestIds.shellTopBar)).toContainText('OPS');
});

test('RootRedirect_WhenInNoProjects_ShouldOpenTheNoProjectsState', async ({ mount, page }) => {
  await stubInstance(page);
  await stubJson(page, '/api/me', meDto({ hasProjects: false }));
  await mount(<AppRoutes />, { hooksConfig: { route: '/' } });
  await expect(page.getByTestId(ids.noProjects)).toBeVisible();
});
