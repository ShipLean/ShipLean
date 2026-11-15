import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import type { AgentSetupDto } from '@shiplean/core/api/inspection';
import type { ProjectDto } from '@shiplean/core/api/projects';
import { agentSetup } from '../../ct/inspectionMocks';
import { AgentSetup } from '../../AgentSetup';
import { InspectionSettings } from '../../InspectionSettings';
import { inspectionTestIds as ids } from '../../testIds';

const project = { id: 'p1', key: 'SHIP', role: 'admin' } as ProjectDto;

const byAgent: Record<string, AgentSetupDto> = {
  'claude-code': agentSetup,
  'codex-cli': {
    agent: 'codex-cli',
    agentLabel: 'Codex CLI',
    files: [
      { name: 'config.toml', language: 'toml', content: '[otel]\nenvironment = "shiplean"\n' },
    ],
    notes: ['Codex CLI note'],
    docsUrl: 'https://docs.shiplean.app/inspection/codex-cli',
  },
  cursor: {
    agent: 'cursor',
    agentLabel: 'Cursor',
    files: [
      { name: '.cursor/hooks.json', language: 'json', content: '{}' },
      { name: '.shiplean', language: 'ini', content: 'item=SHIP-9\n' },
    ],
    notes: ['Cursor note'],
    docsUrl: 'https://docs.shiplean.app/inspection/cursor',
  },
};

async function stub(page: Page) {
  const urls: string[] = [];
  await page.route('**/api/inspection/projects/*/agent-setup*', (route) => {
    const url = new URL(route.request().url());
    urls.push(url.search);
    return route.fulfill({ json: byAgent[url.searchParams.get('agent') ?? ''] });
  });
  await page.route('**/api/inspection/projects/*/settings', (route) =>
    route.fulfill({
      json: {
        enabled: true,
        showContent: false,
        tenantOverride: null,
        selectorOverride: null,
        storeMode: 'tempoLoki',
      },
    }),
  );
  return urls;
}

test('AgentSetup_PerAgentTab_ShouldShowThatAgentsFilesAndNotes', async ({ mount, page }) => {
  await stub(page);
  const view = await mount(<AgentSetup project={project} />);
  await expect(view.getByTestId(ids.inspectionSetupFile)).toHaveAttribute(
    'data-name',
    'settings.json',
  );

  await view.locator(`[data-agent="codex-cli"]`).click();
  await expect(view.getByTestId(ids.inspectionSetupFile)).toHaveAttribute(
    'data-name',
    'config.toml',
  );
  await expect(view.getByTestId(ids.inspectionSetupNotes)).toContainText('Codex CLI note');

  await view.locator(`[data-agent="cursor"]`).click();
  await expect(view.getByTestId(ids.inspectionSetupFile)).toHaveCount(2);
  await expect(view.getByTestId(ids.inspectionSetupNotes)).toContainText('Cursor note');
});

test('AgentSetup_WhenItemTyped_ShouldAskForThatItem', async ({ mount, page }) => {
  const urls = await stub(page);
  const view = await mount(<AgentSetup project={project} />);
  await view.getByTestId(ids.inspectionItemInput).fill('SHIP-9');
  await view.getByTestId(ids.inspectionItemInput).press('Enter');

  await expect.poll(() => urls.some((u) => u.includes('item=SHIP-9'))).toBe(true);
});

test('AgentSetup_CopyButton_ShouldPutTheFileOnTheClipboard', async ({ mount, page, context }) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  await stub(page);
  const view = await mount(<AgentSetup project={project} initialAgent="cursor" />);
  await view.getByTestId(ids.inspectionSetupCopy).nth(1).click();

  await expect(view.getByTestId(ids.inspectionSetupCopy).nth(1)).toHaveText('Copied');
  expect(await page.evaluate(() => navigator.clipboard.readText())).toBe('item=SHIP-9\n');
});

test('Settings_WithAgentParam_ShouldOpenAgentSetupOnThatAgent', async ({ mount, page }) => {
  await stub(page);
  const view = await mount(<InspectionSettings project={project} />, {
    hooksConfig: { route: '/p/SHIP/settings/inspection?agent=cursor' },
  });

  await expect(view.getByTestId(ids.inspectionAgentSetup)).toBeVisible();
  await expect(view.locator(`[data-agent="cursor"]`)).toHaveAttribute('aria-selected', 'true');
  await expect(view.getByTestId(ids.inspectionSetupNotes)).toContainText('Cursor note');
});

test('Settings_WithoutAgentParam_ShouldOpenOnGeneral', async ({ mount, page }) => {
  await stub(page);
  const view = await mount(<InspectionSettings project={project} />);

  await expect(view.getByTestId(ids.inspectionAgentSetup)).toHaveCount(0);
  await expect(view.getByTestId(ids.inspectionSubTabGeneral)).toHaveAttribute(
    'aria-selected',
    'true',
  );
});
