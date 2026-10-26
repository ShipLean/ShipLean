import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import { json, project } from '../ct/fixtures';
import { importTestIds as ids } from '../testIds';
import { CreateProjectFirst } from './CreateProjectFirst';

async function stubKeyCheck(page: Page) {
  await page.route('**/api/projects/key-check**', (route) => {
    const key = new URL(route.request().url()).searchParams.get('key') ?? '';
    if (key === 'SHIP')
      return json(route, { key, available: false, reason: 'taken', alternatives: ['SHP'] });
    if (!/^[A-Z]{2,4}$/.test(key))
      return json(route, { key, available: false, reason: 'invalid', alternatives: [] });
    return json(route, { key, available: true, reason: null, alternatives: [] });
  });
}

test('CreateProjectFirst_WhenKeyInvalid_ShouldBlockNext', async ({ mount, page }) => {
  await stubKeyCheck(page);
  await mount(<CreateProjectFirst />);
  await page.getByTestId(ids.createName).fill('Platform Team');
  await page.getByTestId(ids.createKey).fill('PL4T');
  await expect(page.getByTestId(ids.createKeyResult)).toHaveAttribute('data-available', 'false');
  await expect(page.getByTestId(ids.createNext)).toBeDisabled();
});

test('CreateProjectFirst_WhenKeyTaken_ShouldShowAlternatives', async ({ mount, page }) => {
  await stubKeyCheck(page);
  await mount(<CreateProjectFirst />);
  await page.getByTestId(ids.createName).fill('Platform Team');
  await page.getByTestId(ids.createKey).fill('ship');
  await expect(page.getByTestId(ids.createKeyResult)).toContainText('already used');
  await expect(page.getByTestId(ids.createKeyResult)).toContainText('SHP');
  await expect(page.getByTestId(ids.createNext)).toBeDisabled();
});

test('CreateProjectFirst_WhenKeyValid_ShouldCreateWithoutFirstSprint', async ({ mount, page }) => {
  await stubKeyCheck(page);
  let body: Record<string, unknown> | undefined;
  await page.route('**/api/projects', (route) => {
    if (route.request().method() !== 'POST') return route.fallback();
    body = route.request().postDataJSON() as Record<string, unknown>;
    return json(route, { ...project, key: 'PLAT', name: 'Platform Team' });
  });
  await mount(<CreateProjectFirst />, { hooksConfig: { route: '/import' } });
  await expect(page.getByTestId(ids.createNext)).toBeDisabled();
  await page.getByTestId(ids.createName).fill('Platform Team');
  await page.getByTestId(ids.createKey).fill('plat');
  await expect(page.getByTestId(ids.createKeyResult)).toHaveAttribute('data-available', 'true');
  await expect(page.getByText("We'll create PLAT first")).toBeVisible();
  await page.getByTestId(ids.createNext).click();
  await expect
    .poll(() => body)
    .toMatchObject({
      name: 'Platform Team',
      key: 'PLAT',
      firstSprint: null,
      copyFromProjectId: null,
    });
});

test('CreateProjectFirst_WhenCreateRefused_ShouldShowTheReason', async ({ mount, page }) => {
  await stubKeyCheck(page);
  await page.route('**/api/projects', (route) =>
    route.request().method() === 'POST'
      ? json(route, { error: 'Only instance admins create projects.', code: 'forbidden' }, 403)
      : route.fallback(),
  );
  await mount(<CreateProjectFirst />);
  await page.getByTestId(ids.createName).fill('Platform Team');
  await page.getByTestId(ids.createKey).fill('PLAT');
  await expect(page.getByTestId(ids.createNext)).toBeEnabled();
  await page.getByTestId(ids.createNext).click();
  await expect(page.getByTestId(ids.createError)).toHaveText(
    'Only instance admins create projects.',
  );
});

test('CreateProjectFirst_WhenEarlierImportsExist_ShouldOfferReRunInTheirProject', async ({
  mount,
  page,
}) => {
  await stubKeyCheck(page);
  await page.route('**/api/import/jobs', (route) =>
    json(route, [
      {
        id: 'j1',
        jiraProjectKey: 'MOBILE',
        projectKey: 'MOB',
        projectId: 'p-mob',
        status: 'done',
        progress: {},
        startedAt: null,
        startedLabel: null,
        finishedAt: null,
        finishedLabel: '2 days ago',
        createdBy: null,
      },
    ]),
  );
  await mount(<CreateProjectFirst />);
  await expect(page.getByTestId(ids.earlierImport)).toContainText('MOBILE → MOB');
  await expect(page.getByTestId(ids.earlierImport).getByRole('link')).toHaveAttribute(
    'href',
    '/p/MOB/import',
  );
});
