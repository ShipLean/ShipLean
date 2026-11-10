import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../AppRoutes';
import { appTestIds } from '../../testIds';
import { meDto, projectSummary, stubInstance, stubJson } from './authFixtures';
import { authTestIds as ids } from './testIds';

async function fillAndSubmit(page: {
  getByTestId(id: string): { fill(value: string): Promise<void>; click(): Promise<void> };
}) {
  await page.getByTestId(ids.signUpName).fill('Play Reviewer');
  await page.getByTestId(ids.email).fill('reviewer@example.test');
  await page.getByTestId(ids.password).fill('a-reviewer-password');
  await page.getByTestId(ids.signUpSubmit).click();
}

test('SignUpPage_WhenCreated_ShouldOpenTheAppSignedIn', async ({ mount, page }) => {
  await stubInstance(page);
  await stubJson(page, '/api/auth/signup', meDto({ name: 'Play Reviewer' }));
  await stubJson(page, '/api/me', meDto({ name: 'Play Reviewer' }));
  await stubJson(page, '/api/projects', [projectSummary('SHIP')]);
  await mount(<AppRoutes />, { hooksConfig: { route: '/signup' } });
  await fillAndSubmit(page);
  await expect(page.getByTestId(appTestIds.shellTopBar)).toContainText('SHIP');
});

test('SignUpPage_WhenSignUpIsOff_ShouldShowTheServersSentence', async ({ mount, page }) => {
  await stubInstance(page);
  await stubJson(
    page,
    '/api/auth/signup',
    { error: 'Sign-up is turned off on this ShipLean.', code: 'not_found' },
    404,
  );
  await mount(<AppRoutes />, { hooksConfig: { route: '/signup' } });
  await fillAndSubmit(page);
  await expect(page.getByTestId(ids.failure)).toHaveText('Sign-up is turned off on this ShipLean.');
  await expect(page.getByTestId(ids.signUpForm)).toBeVisible();
});
