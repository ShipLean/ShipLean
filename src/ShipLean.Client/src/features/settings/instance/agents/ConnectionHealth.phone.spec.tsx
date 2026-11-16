import { expect, test } from '@playwright/experimental-ct-react';
import { settingsTestIds as ids } from '../../testIds';
import { failingConnection } from './ct/fixtures';
import { ConnectionRow } from './ConnectionRow';
import type { AgentConnectionDto } from './model';

// The settings row on a phone (390 x 844): health, the failure text and Test connection wrap inside the screen.

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

test('ConnectionHealth_OnAPhone_ShouldFitTheScreenWithTheTestAnswer', async ({ mount, page }) => {
  await page.route('**/api/instance/agent-connections/c-1/health', (route) =>
    route.fulfill({ json: failingConnection }),
  );
  await page.route('**/api/instance/agent-connections/c-1/test', (route) =>
    route.fulfill({
      json: { reached: true, httpStatus: 502, error: null, durationMs: 85 },
    }),
  );
  const view = await mount(
    <div className="p-4">
      <ConnectionRow connection={relay} onShown={() => undefined} />
    </div>,
  );

  await expect(view.getByTestId(ids.agentsConnectionHealth)).toContainText(
    '3 failed in the last 24 h',
  );
  await view.getByTestId(ids.agentsConnectionTest).click();
  await expect(view.getByTestId(ids.agentsConnectionTestResult)).toHaveText(
    'Answered 502 BadGateway',
  );

  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
  expect(overflow).toBeLessThanOrEqual(0);
  await expect(view).toHaveScreenshot('connection-health-phone.png', { maxDiffPixels: 100 });
});
