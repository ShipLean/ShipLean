import { expect, test } from '@playwright/experimental-ct-react';
import settings from '../../../../../ShipLean.TestSupport/DtoFixtures/demos/project-demo-settings.json' with { type: 'json' };
import type { ProjectDto } from '@shiplean/core/api/projects';
import { componentTestIds } from '../../../components/testIds';
import { demosTestIds as ids } from '../../demos/testIds';
import { SettingsDemosHarness } from './ct/SettingsDemosHarness';
import { SettingsDemos } from './SettingsDemos';

const project = { id: 'p-ship', key: 'SHIP', role: 'admin' } as ProjectDto;

test('Panel_ShouldMountBrandAndYouTubePanels', async ({ mount, page }) => {
  await page.route('**/api/projects/*/demo-settings', (route) => route.fulfill({ json: settings }));
  await mount(<SettingsDemos project={project} />);

  await expect(page.getByTestId(ids.demoSettingsPanel)).toBeVisible();
  await expect(page.getByTestId(ids.demoBrandPanel)).toBeVisible();
  await expect(page.getByTestId(ids.youtubePanel)).toBeVisible();
});

const blocked = {
  ...settings,
  agentCheck: {
    ready: false,
    block: 'noConnection',
    message: "This project has no agent connection. Choose one in the project's General settings.",
  },
};

test('Automatic_WhenCheckFails_ShouldBeDisabledAndSayWhyWithALink', async ({ mount, page }) => {
  await page.route('**/api/projects/*/demo-settings', (route) => route.fulfill({ json: blocked }));
  await mount(<SettingsDemos project={project} />, {
    hooksConfig: { route: '/p/SHIP/settings/demos' },
  });

  await expect(page.getByTestId(ids.demoSettingsAutomatic)).toBeDisabled();
  const why = page.getByTestId(ids.demoSettingsAutomaticBlocked);
  await expect(why).toContainText('This project has no agent connection');
  await expect(why.getByRole('link')).toHaveAttribute('href', '/p/SHIP/settings/general');
});

test('Automatic_WhenReady_ShouldToggle', async ({ mount, page }) => {
  await page.route('**/api/projects/*/demo-settings', (route) => route.fulfill({ json: settings }));
  await mount(<SettingsDemos project={project} />, {
    hooksConfig: { route: '/p/SHIP/settings/demos' },
  });
  const automatic = page.getByTestId(ids.demoSettingsAutomatic);

  await expect(automatic).toBeEnabled();
  await expect(automatic).not.toBeChecked();
  await automatic.check();
  await expect(automatic).toBeChecked();
  await expect(page.getByTestId(ids.demoSettingsAutomaticBlocked)).toHaveCount(0);
});

test('Save_ShouldSendTheFieldsAndToastOnFailure', async ({ mount, page }) => {
  const puts: unknown[] = [];
  let fail = true;
  await page.route('**/api/projects/*/demo-settings', (route) => {
    if (route.request().method() !== 'PUT') return route.fulfill({ json: settings });
    puts.push(route.request().postDataJSON());
    return fail
      ? route.fulfill({
          status: 400,
          json: {
            error: 'The environment text is wrong.',
            code: 'validation',
            field: 'environment',
          },
        })
      : route.fulfill({ json: { ...settings, automatic: true, environment: 'make dev' } });
  });
  await mount(<SettingsDemosHarness project={project} />, {
    hooksConfig: { route: '/p/SHIP/settings/demos' },
  });

  await page.getByTestId(ids.demoSettingsAutomatic).check();
  await page.getByTestId(ids.demoSettingsEnvironment).fill('make dev');
  await page.getByTestId(ids.demoSettingsSave).click();
  await expect(page.getByTestId(componentTestIds.toast)).toContainText(
    'The environment text is wrong.',
  );
  fail = false;
  await page.getByTestId(ids.demoSettingsSave).click();

  await expect.poll(() => puts.length).toBe(2);
  expect(puts[1]).toEqual({ automatic: true, environment: 'make dev' });
  await expect(page.getByTestId(ids.demoSettingsEnvironment)).toHaveValue('make dev');
});

test('ReadOnly_WhenMember_ShouldDisableEverything', async ({ mount, page }) => {
  await page.route('**/api/projects/*/demo-settings', (route) => route.fulfill({ json: settings }));
  await mount(<SettingsDemos project={{ ...project, role: 'member' } as ProjectDto} />, {
    hooksConfig: { route: '/p/SHIP/settings/demos' },
  });

  await expect(page.getByTestId(ids.demoSettingsAutomatic)).toBeDisabled();
  await expect(page.getByTestId(ids.demoSettingsEnvironment)).toBeDisabled();
  await expect(page.getByTestId(ids.demoSettingsSave)).toBeDisabled();
});

test('Environment_ShouldExplainItNeverRunsAgainstProduction', async ({ mount, page }) => {
  await page.route('**/api/projects/*/demo-settings', (route) => route.fulfill({ json: settings }));
  await mount(<SettingsDemos project={project} />, {
    hooksConfig: { route: '/p/SHIP/settings/demos' },
  });

  await expect(page.getByTestId(ids.demoSettingsPanel)).toContainText(
    "How an agent starts a local seeded copy of this project. It's never run against production.",
  );
});
