import { expect, test } from '@playwright/experimental-ct-react';
import type { ConsentRequestDto } from '../../../api/agents';
import { agentsTestIds as ids } from '../testIds';
import type { StubbablePage } from './stubPage';
import { ConsentCard } from './ConsentCard';

function request(overrides: Partial<ConsentRequestDto> = {}): ConsentRequestDto {
  return {
    requestId: 'r-1',
    clientName: 'Claude Code',
    clientHost: 'claude.ai',
    verified: true,
    redirectHost: 'agent.example.test',
    requestedScopes: ['shiplean:read', 'shiplean:write', 'shiplean:plan'],
    localhostOnly: false,
    ...overrides,
  };
}

async function stubRequest(page: StubbablePage, body: unknown, status = 200) {
  await page.route('**/api/agents/consent/r-1', (route) => {
    if (route.request().method() !== 'GET') return route.fallback();
    return route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });
  });
}

// Answers the decision POST with a redirect address and keeps what was posted.
async function stubDecision(page: StubbablePage, redirectUrl: string) {
  const posted: { body: unknown } = { body: null };
  await page.route('**/api/agents/consent/r-1', (route) => {
    if (route.request().method() !== 'POST') return route.fallback();
    posted.body = route.request().postDataJSON();
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ redirectUrl }),
    });
  });
  await page.route('http://agent.example.test/**', (route) =>
    route.fulfill({ status: 200, contentType: 'text/html', body: '<html></html>' }),
  );
  return posted;
}

test('ConsentCard_WhenOpened_ShouldTickTheRequestedScopesAndLockRead', async ({ mount, page }) => {
  await stubRequest(page, request({ requestedScopes: ['shiplean:read', 'shiplean:write'] }));
  await mount(<ConsentCard requestId="r-1" />);

  await expect(page.getByTestId(ids.consentWants)).toHaveText(
    'Wants to: read your projects · capture and update work',
  );
  await expect(page.getByTestId(ids.consentScopeRead)).toBeChecked();
  await expect(page.getByTestId(ids.consentScopeRead)).toBeDisabled();
  await expect(page.getByTestId(ids.consentScopeWrite)).toBeChecked();
  await expect(page.getByTestId(ids.consentScopePlan)).not.toBeChecked();
  await expect(page.getByTestId(ids.consentScopePlan)).toBeDisabled();
});

test('ConsentCard_WhenAllowedWithAScopeUnticked_ShouldPostOnlyTheTickedOnes', async ({
  mount,
  page,
}) => {
  await stubRequest(page, request());
  const posted = await stubDecision(page, 'http://agent.example.test/cb?code=c1&iss=i');
  await mount(<ConsentCard requestId="r-1" />);

  await page.getByTestId(ids.consentScopePlan).uncheck();
  await page.getByTestId(ids.consentAllow).click();

  await page.waitForURL('http://agent.example.test/cb?code=c1&iss=i');
  expect(posted.body).toEqual({ allow: true, scopes: ['shiplean:read', 'shiplean:write'] });
});

test('ConsentCard_WhenDenied_ShouldPostDenyAndFollowTheReturnedAddress', async ({
  mount,
  page,
}) => {
  await stubRequest(page, request());
  const posted = await stubDecision(page, 'http://agent.example.test/cb?error=access_denied');
  await mount(<ConsentCard requestId="r-1" />);

  await page.getByTestId(ids.consentDeny).click();

  await page.waitForURL('http://agent.example.test/cb?error=access_denied');
  expect(posted.body).toEqual({ allow: false, scopes: [] });
});

test('ConsentCard_WhenReturnAddressIsNotWeb_ShouldNotNavigate', async ({ mount, page }) => {
  await stubRequest(page, request());
  await stubDecision(page, 'javascript:alert(1)');
  await mount(<ConsentCard requestId="r-1" />);
  const before = page.url();

  await page.getByTestId(ids.consentAllow).click();

  await expect(page.getByTestId(ids.consentError)).toContainText('return address');
  expect(page.url()).toBe(before);
});

test('ConsentCard_WhenVerified_ShouldShowTheHost', async ({ mount, page }) => {
  await stubRequest(page, request());
  await mount(<ConsentCard requestId="r-1" />);

  await expect(page.getByTestId(ids.consentHost)).toHaveText('claude.ai');
  await expect(page.getByTestId(ids.consentHost)).toHaveAttribute('data-verified', 'true');
  await expect(page.getByTestId(ids.consentRedirectHost)).toHaveText('agent.example.test');
});

test('ConsentCard_WhenUnverified_ShouldSayUnverified', async ({ mount, page }) => {
  await stubRequest(page, request({ verified: false, clientHost: null }));
  await mount(<ConsentCard requestId="r-1" />);

  await expect(page.getByTestId(ids.consentHost)).toHaveText('unverified');
  await expect(page.getByTestId(ids.consentHost)).toHaveAttribute('data-verified', 'false');
});

test('ConsentCard_WhenClientNameHasMarkup_ShouldShowItAsText', async ({ mount, page }) => {
  const name = '<img src=x onerror="window.pwned=1"><b>Evil</b>';
  await stubRequest(page, request({ clientName: name }));
  await mount(<ConsentCard requestId="r-1" />);

  await expect(page.getByTestId(ids.consentClient)).toHaveText(name);
  await expect(page.getByTestId(ids.consentCard).locator('img')).toHaveCount(0);
});

test('ConsentCard_WhenLocalhostOnly_ShouldWarn', async ({ mount, page }) => {
  await stubRequest(page, request({ localhostOnly: true }));
  await mount(<ConsentCard requestId="r-1" />);
  await expect(page.getByTestId(ids.consentLocalhostWarning)).toContainText('localhost');
});

test('ConsentCard_WhenNotLocalhostOnly_ShouldNotWarn', async ({ mount, page }) => {
  await stubRequest(page, request());
  await mount(<ConsentCard requestId="r-1" />);
  await expect(page.getByTestId(ids.consentAllow)).toBeVisible();
  await expect(page.getByTestId(ids.consentLocalhostWarning)).toHaveCount(0);
});

test('ConsentCard_WhenRequestUnknown_ShouldSayItExpired', async ({ mount, page }) => {
  await stubRequest(page, { error: 'No such authorization request.', code: 'not_found' }, 404);
  await mount(<ConsentCard requestId="r-1" />);
  await expect(page.getByTestId(ids.consentError)).toContainText('expired');
  await expect(page.getByTestId(ids.consentAllow)).toHaveCount(0);
});

test('ConsentCard_WhenNoRequestId_ShouldSendThePersonBackToTheAgent', async ({ mount, page }) => {
  await mount(<ConsentCard requestId={null} />);
  await expect(page.getByTestId(ids.consentError)).toContainText('opens from your agent');
});

test('ConsentCard_WhenPrivateUseRedirect_ShouldShowTheSchemeAndLeave', async ({ mount, page }) => {
  await stubRequest(
    page,
    request({
      verified: false,
      clientHost: null,
      redirectHost: 'cursor://anysphere.cursor-retrieval',
    }),
  );
  await stubDecision(page, 'cursor://anysphere.cursor-retrieval/oauth/callback?code=c1&iss=i');
  await mount(<ConsentCard requestId="r-1" />);

  await expect(page.getByTestId(ids.consentRedirectHost)).toHaveText(
    'cursor://anysphere.cursor-retrieval',
  );
  await expect(page.getByTestId(ids.consentRedirectApp)).toContainText('an app on this computer');
  await expect(page.getByTestId(ids.consentHost)).toHaveText('unverified');
  await expect(page.getByTestId(ids.consentLocalhostWarning)).toHaveCount(0);

  await page.getByTestId(ids.consentAllow).click();

  // The browser hands the address to the app, so the page stays: it says it is leaving and does not refuse.
  await expect(page.getByTestId(ids.consentLeaving)).toHaveText(
    'Returning to cursor://anysphere.cursor-retrieval',
  );
  await expect(page.getByTestId(ids.consentError)).toHaveCount(0);
});

test('ConsentCard_WhenWebRedirect_ShouldNotShowTheAppLabel', async ({ mount, page }) => {
  await stubRequest(page, request());
  await mount(<ConsentCard requestId="r-1" />);

  await expect(page.getByTestId(ids.consentRedirectHost)).toHaveText('agent.example.test');
  await expect(page.getByTestId(ids.consentRedirectApp)).toHaveCount(0);
});
