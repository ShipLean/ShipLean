import { expect, test } from '@playwright/experimental-ct-react';
import { connection, json, project, stubImport } from '../ct/fixtures';
import { importTestIds as ids } from '../testIds';
import { ConnectionPanel } from './ConnectionPanel';

test('ConnectionPanel_WhenConnectionSaved_ShouldShowEmailAndLastFourOnly', async ({
  mount,
  page,
}) => {
  await stubImport(page, connection);
  await mount(<ConnectionPanel projectId={project.id} />);
  await expect(page.getByTestId(ids.tokenSaved)).toHaveText('Token saved, ends 3fA9');
  await expect(page.getByTestId(ids.connectionSummary)).toContainText('priya@acme.io');
  await expect(page.getByTestId(ids.token)).toHaveCount(0);
  await expect(page.getByTestId(ids.save)).toHaveCount(0);
});

test('ConnectionPanel_WhenReplaceClicked_ShouldAskForANewToken', async ({ mount, page }) => {
  await stubImport(page, connection);
  await mount(<ConnectionPanel projectId={project.id} />);
  await page.getByTestId(ids.replace).click();
  await expect(page.getByTestId(ids.token)).toBeVisible();
  await expect(page.getByTestId(ids.test)).toBeDisabled();
});

test('ConnectionPanel_WhenNoneSaved_ShouldSaveWhatWasTyped', async ({ mount, page }) => {
  await stubImport(page, null);
  let body: unknown;
  await page.route(`**/api/projects/${project.id}/jira/connection`, async (route) => {
    if (route.request().method() !== 'PUT') return route.fallback();
    body = route.request().postDataJSON();
    return json(route, connection);
  });
  await mount(<ConnectionPanel projectId={project.id} />);
  await expect(page.getByTestId(ids.save)).toBeDisabled();
  await page.getByTestId(ids.baseUrl).fill('https://acme.atlassian.net');
  await page.getByTestId(ids.email).fill('priya@acme.io');
  await page.getByTestId(ids.token).fill('secret-token-3fA9');
  await page.getByTestId(ids.save).click();
  await expect
    .poll(() => body)
    .toEqual({
      baseUrl: 'https://acme.atlassian.net',
      kind: 'cloud',
      email: 'priya@acme.io',
      token: 'secret-token-3fA9',
    });
});

test('ConnectionPanel_WhenServerKind_ShouldAskForATokenOnly', async ({ mount, page }) => {
  await stubImport(page, null);
  await mount(<ConnectionPanel projectId={project.id} />);
  await page.getByTestId(ids.kindServer).click();
  await expect(page.getByTestId(ids.email)).toHaveCount(0);
  await page.getByTestId(ids.baseUrl).fill('https://jira.acme.net');
  await page.getByTestId(ids.token).fill('pat');
  await expect(page.getByTestId(ids.save)).toBeEnabled();
});

test('ConnectionPanel_WhenSaveRefused_ShouldShowTheReason', async ({ mount, page }) => {
  await stubImport(page, null);
  await page.route(`**/api/projects/${project.id}/jira/connection`, (route) =>
    route.request().method() === 'PUT'
      ? json(route, { error: 'That address is private.', code: 'validation' }, 400)
      : route.fallback(),
  );
  await mount(<ConnectionPanel projectId={project.id} />);
  await page.getByTestId(ids.baseUrl).fill('https://10.0.0.1');
  await page.getByTestId(ids.email).fill('a@b.io');
  await page.getByTestId(ids.token).fill('t');
  await page.getByTestId(ids.save).click();
  await expect(page.getByTestId(ids.saveError)).toHaveText('That address is private.');
});

test('ConnectionPanel_WhenTestFails_ShouldShowWhatToCheck', async ({ mount, page }) => {
  await stubImport(page, connection);
  await page.route(`**/api/projects/${project.id}/jira/connection/test`, (route) =>
    json(route, {
      ok: false,
      who: null,
      message: 'Jira rejected the token. Check it has not expired.',
    }),
  );
  await mount(<ConnectionPanel projectId={project.id} />);
  await page.getByTestId(ids.test).click();
  await expect(page.getByTestId(ids.testResult)).toContainText('What to check');
  await expect(page.getByTestId(ids.testResult)).toContainText('Check it has not expired.');
  await expect(page.getByTestId(ids.testResult)).toHaveAttribute('data-ok', 'false');
});

test('ConnectionPanel_WhenTestWorks_ShouldShowWho', async ({ mount, page }) => {
  await stubImport(page, connection);
  await page.route(`**/api/projects/${project.id}/jira/connection/test`, (route) =>
    json(route, {
      ok: true,
      who: { displayName: 'Priya Shah', email: 'priya@acme.io' },
      message: null,
    }),
  );
  await mount(<ConnectionPanel projectId={project.id} />);
  await page.getByTestId(ids.test).click();
  await expect(page.getByTestId(ids.testResult)).toContainText('Connected as Priya Shah');
});

test('ConnectionPanel_WhenSaved_ShouldClearTheTokenAndNeedANewTest', async ({ mount, page }) => {
  await stubImport(page, null);
  const tested: (boolean | null)[] = [];
  let saved = false;
  await page.route(`**/api/projects/${project.id}/jira/connection`, async (route) => {
    const method = route.request().method();
    if (method === 'PUT') {
      saved = true;
      return json(route, connection);
    }
    return saved ? json(route, connection) : route.fulfill({ status: 204 });
  });
  await page.route(`**/api/projects/${project.id}/jira/connection/test`, (route) =>
    json(route, { ok: true, who: null, message: null }),
  );
  await mount(
    <ConnectionPanel projectId={project.id} onTested={(r) => tested.push(r?.ok ?? null)} />,
  );
  await page.getByTestId(ids.baseUrl).fill('https://acme.atlassian.net');
  await page.getByTestId(ids.email).fill('priya@acme.io');
  await page.getByTestId(ids.token).fill('secret-token-3fA9');
  await page.getByTestId(ids.save).click();
  await expect(page.getByTestId(ids.tokenSaved)).toBeVisible();
  await page.getByTestId(ids.test).click();
  await expect(page.getByTestId(ids.testResult)).toHaveAttribute('data-ok', 'true');
  await page.getByTestId(ids.replace).click();
  await expect(page.getByTestId(ids.token)).toHaveValue('');
  await page.getByTestId(ids.token).fill('another-token');
  await page.getByTestId(ids.save).click();
  await expect(page.getByTestId(ids.tokenSaved)).toBeVisible();
  await expect(page.getByTestId(ids.testResult)).toHaveCount(0);
  expect(tested).toEqual([null, true, null]);
});
