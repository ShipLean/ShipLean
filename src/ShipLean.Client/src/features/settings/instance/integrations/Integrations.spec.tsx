import { expect, test } from '@playwright/experimental-ct-react';
import { settingsTestIds as ids } from '../../testIds';
import type { IntegrationsSaveDto, IntegrationsSettingsDto } from './model';
import { InstanceIntegrations } from './InstanceIntegrations';

interface Routable {
  route(
    url: string,
    handler: (route: {
      request(): { method(): string; postDataJSON(): unknown };
      fulfill(response: { json?: unknown; status?: number }): Promise<void>;
    }) => Promise<void>,
  ): Promise<unknown>;
}

const empty: IntegrationsSettingsDto = {
  gitHubTokenLastFour: null,
  gitHubTokenFromEnv: false,
  gitLabUrl: null,
  gitLabTokenLastFour: null,
  gitLabFromEnv: false,
  previewAllowHosts: [],
};

// A server that applies a PUT the way the real one reports it: a typed token comes back as its last four only.
const stub = async (page: Routable, start: IntegrationsSettingsDto = empty) => {
  const puts: IntegrationsSaveDto[] = [];
  let current = start;
  await page.route('**/api/instance/integrations', async (route) => {
    const request = route.request();
    if (request.method() === 'PUT') {
      const body = request.postDataJSON() as IntegrationsSaveDto;
      puts.push(body);
      current = {
        ...current,
        gitHubTokenLastFour: body.gitHubToken
          ? body.gitHubToken.slice(-4)
          : current.gitHubTokenLastFour,
        previewAllowHosts: body.previewAllowHosts,
      };
    }
    return route.fulfill({ json: current });
  });
  return puts;
};

test('Integrations_WhenTokenSaved_ShouldShowLastFour', async ({ mount, page }) => {
  const puts = await stub(page);
  const view = await mount(<InstanceIntegrations />);

  await view.getByTestId(ids.integrationsGitHubTokenInput).fill('ghp_topsecret1234');
  await view.getByTestId(ids.integrationsSave).click();

  await expect(view.getByTestId(ids.integrationsGitHubTokenLastFour)).toHaveText('•••• 1234');
  await expect(view.getByTestId(ids.integrationsGitHubTokenInput)).toHaveCount(0);
  await expect(view.getByTestId(ids.integrationsDirty)).toHaveText('Saved');
  expect(await view.textContent()).not.toContain('topsecret');
  expect(puts).toEqual([
    {
      gitHubToken: 'ghp_topsecret1234',
      clearGitHubToken: false,
      clearGitLabToken: false,
      previewAllowHosts: [],
    },
  ]);
});

test('Integrations_WhenClearClicked_ShouldSendOnlyThatChange', async ({ mount, page }) => {
  const puts = await stub(page, { ...empty, gitHubTokenLastFour: '1234' });
  const view = await mount(<InstanceIntegrations />);

  await view.getByTestId(ids.integrationsGitHubTokenClear).click();
  await view.getByTestId(ids.integrationsSave).click();

  await expect(view.getByTestId(ids.integrationsDirty)).toHaveText('Saved');
  expect(puts).toEqual([
    { clearGitHubToken: true, clearGitLabToken: false, previewAllowHosts: [] },
  ]);
});

test('Integrations_WhenTokenComesFromEnvironment_ShouldSaySoAndOfferNoClear', async ({
  mount,
  page,
}) => {
  await stub(page, { ...empty, gitHubTokenLastFour: '0001', gitHubTokenFromEnv: true });
  const view = await mount(<InstanceIntegrations />);

  await expect(view.getByTestId(ids.integrationsGitHubTokenEnv)).toBeVisible();
  await expect(view.getByTestId(ids.integrationsGitHubTokenReplace)).toBeVisible();
  await expect(view.getByTestId(ids.integrationsGitHubTokenClear)).toHaveCount(0);
});

test('Integrations_WhenGitLabUrlEdited_ShouldHideTheSavedTokenAndSendTheUrl', async ({
  mount,
  page,
}) => {
  const puts = await stub(page, {
    ...empty,
    gitLabUrl: 'https://gitlab.example.com',
    gitLabTokenLastFour: '5678',
  });
  const view = await mount(<InstanceIntegrations />);
  await expect(view.getByTestId(ids.integrationsGitLabTokenLastFour)).toHaveText('•••• 5678');

  await view.getByTestId(ids.integrationsGitLabUrl).fill('https://gitlab.other.example.com');

  await expect(view.getByTestId(ids.integrationsGitLabTokenLastFour)).toHaveCount(0);
  await expect(view.getByTestId(ids.integrationsGitLabTokenInput)).toBeVisible();
  await view.getByTestId(ids.integrationsSave).click();
  await expect(view.getByTestId(ids.integrationsDirty)).toHaveText('Saved');
  expect(puts[0]?.gitLabUrl).toBe('https://gitlab.other.example.com');
});

test('Integrations_WhenAllowListInvalidHost_ShouldShowError', async ({ mount, page }) => {
  await page.route('**/api/instance/integrations', async (route) => {
    if (route.request().method() === 'GET') return route.fulfill({ json: empty });
    return route.fulfill({
      status: 400,
      json: {
        error: 'Each line must be a host name only, such as example.com: no scheme, path or port.',
        code: 'validation',
        field: 'previewAllowHosts',
      },
    });
  });
  const view = await mount(<InstanceIntegrations />);

  await view
    .getByTestId(ids.integrationsAllowHosts)
    .fill('ok.example.com\nhttps://bad.example.com');
  await view.getByTestId(ids.integrationsSave).click();

  await expect(view.getByTestId(ids.integrationsFieldError)).toContainText('host name only');
  await expect(view.getByTestId(ids.integrationsSaveError)).toHaveCount(0);
  await expect(view.getByTestId(ids.integrationsAllowHosts)).toHaveValue(
    'ok.example.com\nhttps://bad.example.com',
  );
});

test('Integrations_WhenSaveFails_ShouldKeepDraftAndShowError', async ({ mount, page }) => {
  await page.route('**/api/instance/integrations', async (route) => {
    if (route.request().method() === 'GET') return route.fulfill({ json: empty });
    return route.fulfill({
      status: 500,
      json: { error: 'Something broke on the server.', code: 'internal_error' },
    });
  });
  const view = await mount(<InstanceIntegrations />);

  await view.getByTestId(ids.integrationsGitHubTokenInput).fill('ghp_topsecret1234');
  await view.getByTestId(ids.integrationsSave).click();

  await expect(view.getByTestId(ids.integrationsSaveError)).toContainText('Something broke');
  await expect(view.getByTestId(ids.integrationsGitHubTokenInput)).toHaveValue('ghp_topsecret1234');
  await expect(view.getByTestId(ids.integrationsDirty)).toHaveText('Unsaved changes');
});

test('Integrations_WhenNotInstanceAdmin_ShouldShowDeniedNotTheForm', async ({ mount, page }) => {
  await page.route('**/api/instance/integrations', (route) =>
    route.fulfill({ status: 403, json: { error: 'Nope.', code: 'forbidden' } }),
  );
  const view = await mount(<InstanceIntegrations />);

  await expect(page.getByTestId(ids.instanceDenied)).toBeVisible();
  await expect(view.getByTestId(ids.integrationsPanel)).toHaveCount(0);
});

test('Integrations_WhenGitLabUrlIsFromEnvironment_ShouldSendItWithANewToken', async ({
  mount,
  page,
}) => {
  const puts = await stub(page, {
    ...empty,
    gitLabUrl: 'https://gitlab.env.example.com',
    gitLabTokenLastFour: '0002',
    gitLabFromEnv: true,
  });
  const view = await mount(<InstanceIntegrations />);

  await view.getByTestId(ids.integrationsGitLabTokenReplace).click();
  await view.getByTestId(ids.integrationsGitLabTokenInput).fill('glpat-newsecret3333');
  await view.getByTestId(ids.integrationsSave).click();

  await expect(view.getByTestId(ids.integrationsDirty)).toHaveText('Saved');
  expect(puts[0]?.gitLabUrl).toBe('https://gitlab.env.example.com');
  expect(puts[0]?.gitLabToken).toBe('glpat-newsecret3333');
});

test('Integrations_WhenReplaceClickedThenDiscarded_ShouldShowTheSavedTokenAgain', async ({
  mount,
  page,
}) => {
  await stub(page, { ...empty, gitHubTokenLastFour: '1234' });
  const view = await mount(<InstanceIntegrations />);

  await view.getByTestId(ids.integrationsGitHubTokenReplace).click();
  await view.getByTestId(ids.integrationsDiscard).click();

  await expect(view.getByTestId(ids.integrationsGitHubTokenLastFour)).toHaveText('•••• 1234');
});
