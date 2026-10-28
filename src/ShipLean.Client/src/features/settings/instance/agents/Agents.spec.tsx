import { expect, test } from '@playwright/experimental-ct-react';
import { settingsTestIds as ids } from '../../testIds';
import type { AgentConnectionDto, AgentConnectionIssuedDto, AgentConnectionPut } from './model';
import { InstanceAgents } from './InstanceAgents';

interface Routable {
  route(
    url: string,
    handler: (route: {
      request(): { method(): string; url(): string; postDataJSON(): unknown };
      fulfill(response: { json?: unknown; status?: number }): Promise<void>;
    }) => Promise<void>,
  ): Promise<unknown>;
}

const SECRET = 'S3cretValueShownOnce000000000000000000000AAAA';
const TOKEN = 'T0kenValueShownOnce0000000000000000000000BBBB';

const relay: AgentConnectionDto = {
  id: 'c-1',
  type: 'linear-compatible',
  name: 'Home relay',
  receiverUrl: 'https://relay.example.com/hook',
  signingSecretLast4: 'a1b2',
  callbackTokenLast4: 'c3d4',
  createdAt: '2026-10-28T09:00:00Z',
  updatedAt: '2026-10-28T09:00:00Z',
  archivedAt: null,
};

// A server holding the list. Reads carry last four only; create and regenerate carry the plaintext, as the real one does.
const stub = async (page: Routable, start: AgentConnectionDto[] = []) => {
  let list = start;
  const calls: string[] = [];
  await page.route('**/api/instance/agent-connections**', async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname.replace('/api/instance/agent-connections', '');
    if (request.method() === 'GET') return route.fulfill({ json: list });
    calls.push(`${request.method()} ${path}`);
    if (path === '') {
      const body = request.postDataJSON() as AgentConnectionPut;
      const connection = { ...relay, id: 'c-new', name: body.name, receiverUrl: body.receiverUrl };
      list = [...list, connection];
      const issued: AgentConnectionIssuedDto = {
        connection,
        signingSecret: SECRET,
        callbackToken: TOKEN,
      };
      return route.fulfill({ json: issued });
    }
    const id = path.split('/')[1];
    const connection = list.find((c) => c.id === id) ?? relay;
    if (path.endsWith('/regenerate-secret'))
      return route.fulfill({ json: { connection, signingSecret: SECRET, callbackToken: null } });
    if (path.endsWith('/regenerate-token'))
      return route.fulfill({ json: { connection, signingSecret: null, callbackToken: TOKEN } });
    if (path.endsWith('/archive')) {
      const archived = { ...connection, archivedAt: '2026-10-28T10:00:00Z' };
      list = list.map((c) => (c.id === id ? archived : c));
      return route.fulfill({ json: archived });
    }
    const body = request.postDataJSON() as AgentConnectionPut;
    const updated = { ...connection, ...body };
    list = list.map((c) => (c.id === id ? updated : c));
    return route.fulfill({ json: updated });
  });
  return calls;
};

test('Agents_WhenConnectionAdded_ShouldShowSecretAndTokenOnceAndThenOnlyLastFour', async ({
  mount,
  page,
}) => {
  await stub(page);
  const view = await mount(<InstanceAgents />);
  await expect(view.getByTestId(ids.agentsEmpty)).toBeVisible();

  await view.getByTestId(ids.agentsAdd).click();
  await view.getByTestId(ids.agentsFormName).fill('Home relay');
  await view.getByTestId(ids.agentsFormUrl).fill('https://relay.example.com/hook');
  await view.getByTestId(ids.agentsFormSave).click();

  await expect(page.getByTestId(ids.agentsShownSecret)).toHaveText(SECRET);
  await expect(page.getByTestId(ids.agentsShownToken)).toHaveText(TOKEN);
  await page.getByTestId(ids.agentsShownDone).click();

  await expect(page.getByTestId(ids.agentsShownOnce)).toHaveCount(0);
  await expect(view.getByTestId(ids.agentsName)).toHaveText('Home relay');
  await expect(view.getByTestId(ids.agentsSecretLast4)).toHaveText('•••• a1b2');
  expect(await view.textContent()).not.toContain(SECRET);
  expect(await view.textContent()).not.toContain(TOKEN);
});

test('Agents_WhenSecretRegenerated_ShouldAskFirstThenShowOnlyTheSecretOnce', async ({
  mount,
  page,
}) => {
  const calls = await stub(page, [relay]);
  const view = await mount(<InstanceAgents />);

  await view.getByTestId(ids.agentsRegenerateSecret).click();
  expect(calls).toEqual([]);
  await view.getByTestId(ids.agentsConfirm).click();

  await expect(page.getByTestId(ids.agentsShownSecret)).toHaveText(SECRET);
  await expect(page.getByTestId(ids.agentsShownToken)).toHaveCount(0);
  expect(calls).toEqual(['POST /c-1/regenerate-secret']);
  await page.getByTestId(ids.agentsShownOnceClose).click();
  await expect(page.getByTestId(ids.agentsShownOnce)).toHaveCount(0);
});

test('Agents_WhenTokenRegenerated_ShouldShowOnlyTheTokenOnce', async ({ mount, page }) => {
  await stub(page, [relay]);
  const view = await mount(<InstanceAgents />);

  await view.getByTestId(ids.agentsRegenerateToken).click();
  await view.getByTestId(ids.agentsConfirm).click();

  await expect(page.getByTestId(ids.agentsShownToken)).toHaveText(TOKEN);
  await expect(page.getByTestId(ids.agentsShownSecret)).toHaveCount(0);
});

test('Agents_WhenRegenerateCancelled_ShouldCallNothing', async ({ mount, page }) => {
  const calls = await stub(page, [relay]);
  const view = await mount(<InstanceAgents />);

  await view.getByTestId(ids.agentsRegenerateToken).click();
  await view.getByTestId(ids.agentsConfirmCancel).click();

  await expect(view.getByTestId(ids.agentsRegenerateToken)).toBeVisible();
  expect(calls).toEqual([]);
});

test('Agents_WhenEdited_ShouldSendNameAndUrl', async ({ mount, page }) => {
  const calls = await stub(page, [relay]);
  const view = await mount(<InstanceAgents />);

  await view.getByTestId(ids.agentsEdit).click();
  await view.getByTestId(ids.agentsFormName).fill('Renamed');
  await view.getByTestId(ids.agentsFormSave).click();

  await expect(view.getByTestId(ids.agentsName)).toHaveText('Renamed');
  expect(calls).toEqual(['PUT /c-1']);
});

test('Agents_WhenArchived_ShouldMoveToArchivedWithNoActions', async ({ mount, page }) => {
  await stub(page, [relay]);
  const view = await mount(<InstanceAgents />);

  await view.getByTestId(ids.agentsArchive).click();
  await view.getByTestId(ids.agentsConfirm).click();

  await expect(view.getByTestId(ids.agentsArchivedBadge)).toBeVisible();
  await expect(view.getByTestId(ids.agentsEdit)).toHaveCount(0);
  await expect(view.getByTestId(ids.agentsRegenerateSecret)).toHaveCount(0);
});

test('Agents_WhenUrlRefused_ShouldShowTheMessageUnderTheField', async ({ mount, page }) => {
  await page.route('**/api/instance/agent-connections', async (route) => {
    if (route.request().method() === 'GET') return route.fulfill({ json: [] });
    return route.fulfill({
      status: 400,
      json: {
        error: 'The receiver URL must be an https address.',
        code: 'validation',
        field: 'receiverUrl',
      },
    });
  });
  const view = await mount(<InstanceAgents />);

  await view.getByTestId(ids.agentsAdd).click();
  await view.getByTestId(ids.agentsFormName).fill('Bad');
  await view.getByTestId(ids.agentsFormUrl).fill('http://relay.example.com');
  await view.getByTestId(ids.agentsFormSave).click();

  await expect(view.getByTestId(ids.agentsFieldError)).toContainText('https');
  await expect(view.getByTestId(ids.agentsFormUrl)).toHaveValue('http://relay.example.com');
  await expect(page.getByTestId(ids.agentsShownOnce)).toHaveCount(0);
});

test('Agents_WhenNotInstanceAdmin_ShouldShowDeniedNotTheList', async ({ mount, page }) => {
  await page.route('**/api/instance/agent-connections', (route) =>
    route.fulfill({ status: 403, json: { error: 'Nope.', code: 'forbidden' } }),
  );
  const view = await mount(<InstanceAgents />);

  await expect(page.getByTestId(ids.instanceDenied)).toBeVisible();
  await expect(view.getByTestId(ids.agentsPanel)).toHaveCount(0);
});
