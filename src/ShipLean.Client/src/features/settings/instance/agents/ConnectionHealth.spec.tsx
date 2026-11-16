import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import { settingsTestIds as ids } from '../../testIds';
import { failingConnection, healthyConnection, testReached, testUnreachable } from './ct/fixtures';
import { ConnectionHealth } from './ConnectionHealth';
import type { AgentConnectionDto, AgentConnectionHealth, AgentConnectionTest } from './model';

// The colour a theme token resolves to, so a dead utility class (which falls back to body ink) turns the test red.
async function tokenColour(page: Page, token: string) {
  return page.evaluate((name) => {
    const probe = document.createElement('span');
    probe.style.color = `var(${name})`;
    document.body.append(probe);
    return getComputedStyle(probe).color;
  }, token);
}

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

// Answers the health read, and the test once `release` is called (so the busy state can be looked at without waiting).
async function stub(page: Page, health: AgentConnectionHealth, answer: AgentConnectionTest) {
  const calls: string[] = [];
  let release: () => void = () => undefined;
  const gate = new Promise<void>((resolve) => (release = resolve));
  await page.route('**/api/instance/agent-connections/c-1/health', (route) =>
    route.fulfill({ json: health }),
  );
  await page.route('**/api/instance/agent-connections/c-1/test', async (route) => {
    calls.push(route.request().method());
    await gate;
    await route.fulfill({ json: answer });
  });
  return { calls, release };
}

test('ConnectionHealth_WhenTheConnectionIsHealthy_ShouldShowWhenItLastDelivered', async ({
  mount,
  page,
}) => {
  await stub(page, { ...healthyConnection, lastDeliveredLabel: '2 hours ago' }, testReached);
  await mount(<ConnectionHealth connection={relay} />);

  await expect(page.getByTestId(ids.agentsConnectionHealth)).toContainText(
    'Last delivered 2 hours ago',
  );
  await expect(page.getByTestId(ids.agentsConnectionHealth)).not.toContainText('failed');
});

test('ConnectionHealth_WhenDeliveriesFailed_ShouldShowTheCountWithItsWindowAndTheLastError', async ({
  mount,
  page,
}) => {
  await stub(page, { ...failingConnection, failedLast24h: 2 }, testReached);
  await mount(<ConnectionHealth connection={relay} />);

  const health = page.getByTestId(ids.agentsConnectionHealth);
  await expect(health).toContainText('2 failed in the last 24 h');
  await expect(health).toContainText('No answer within 5 seconds.');
});

test('ConnectionHealth_WhenNothingWasDelivered_ShouldSaySo', async ({ mount, page }) => {
  await stub(
    page,
    { ...healthyConnection, lastDeliveredAt: null, lastDeliveredLabel: null },
    testReached,
  );
  await mount(<ConnectionHealth connection={relay} />);

  await expect(page.getByTestId(ids.agentsConnectionHealth)).toContainText('No delivery yet');
});

test('ConnectionHealth_WhenTesting_ShouldBeBusyAndDisabledThenShowTheAnswer', async ({
  mount,
  page,
}) => {
  const server = await stub(page, healthyConnection, { ...testReached, httpStatus: 200 });
  const view = await mount(<ConnectionHealth connection={relay} />);
  const button = view.getByTestId(ids.agentsConnectionTest);

  await button.click();
  await expect(button).toBeDisabled();
  await expect(button).toHaveText('Testing…');
  await expect(view.getByTestId(ids.agentsConnectionTestResult)).toHaveCount(0);

  server.release();
  const answered = view.getByTestId(ids.agentsConnectionTestResult);
  await expect(answered).toHaveText('Answered 200 OK');
  await expect(answered).toHaveCSS('color', await tokenColour(page, '--color-ok'));
  await expect(button).toBeEnabled();
  expect(server.calls).toEqual(['POST']);
});

test('ConnectionHealth_WhenTheReceiverAnswersBadGateway_ShouldShowTheStatusAsReached', async ({
  mount,
  page,
}) => {
  const server = await stub(page, healthyConnection, { ...testReached, httpStatus: 502 });
  server.release();
  const view = await mount(<ConnectionHealth connection={relay} />);

  await view.getByTestId(ids.agentsConnectionTest).click();

  const result = view.getByTestId(ids.agentsConnectionTestResult);
  await expect(result).toHaveText('Answered 502 BadGateway');
  await expect(result).not.toContainText('Connection OK');
  // Reached, but not a 2xx: neither the success colour nor the alert colour, just the muted ink.
  await expect(result).toHaveCSS('color', await tokenColour(page, '--color-muted'));
});

test('ConnectionHealth_WhenNothingAnswers_ShouldShowTheError', async ({ mount, page }) => {
  const server = await stub(page, healthyConnection, testUnreachable);
  server.release();
  const view = await mount(<ConnectionHealth connection={relay} />);

  await view.getByTestId(ids.agentsConnectionTest).click();

  const result = view.getByTestId(ids.agentsConnectionTestResult);
  await expect(result).toHaveText('No answer within 5 seconds.');
  await expect(result).toHaveCSS('color', await tokenColour(page, '--color-danger'));
});

test('ConnectionHealth_WhenTheConnectionIsArchived_ShouldNotOfferTheTest', async ({
  mount,
  page,
}) => {
  await stub(page, healthyConnection, testReached);
  const view = await mount(
    <ConnectionHealth connection={{ ...relay, archivedAt: '2026-10-28T10:00:00Z' }} />,
  );

  await expect(page.getByTestId(ids.agentsConnectionHealth)).toBeVisible();
  await expect(view.getByTestId(ids.agentsConnectionTest)).toHaveCount(0);
});

test.describe('on a phone', () => {
  test.use({ viewport: { width: 390, height: 844 } });

  test('ConnectionHealth_WhenTheLastErrorHoldsALongUrl_ShouldNotScrollSideways', async ({
    mount,
    page,
  }) => {
    // One unbroken token: the browser may break a URL after a slash, but not inside a long path segment.
    const url = `https://relay.example.com/${'a'.repeat(120)}`;
    await stub(
      page,
      {
        ...failingConnection,
        lastError: `HttpRequestException: Connection refused posting ${url}`,
      },
      { ...testUnreachable, error: `No answer from ${url}` },
    ).then((server) => server.release());
    const view = await mount(
      <div className="p-4">
        <ConnectionHealth connection={relay} />
      </div>,
    );

    await expect(view.getByTestId(ids.agentsConnectionHealth)).toContainText(url);
    await view.getByTestId(ids.agentsConnectionTest).click();
    await expect(view.getByTestId(ids.agentsConnectionTestResult)).toContainText(url);

    const overflow = await page.evaluate(
      () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
    );
    expect(overflow).toBeLessThanOrEqual(0);
  });
});
