import { expect, test } from '@playwright/experimental-ct-react';
import settings from '../../../../../ShipLean.TestSupport/DtoFixtures/demos/project-demo-settings.json' with { type: 'json' };
import type { ProjectDto } from '../../../api/projects';
import { demosTestIds as ids } from '../../demos/testIds';
import { SettingsDemos } from './SettingsDemos';

const project = { id: 'p-ship', key: 'SHIP', role: 'admin' } as ProjectDto;

test('Panel_ShouldMountBrandAndYouTubePanels', async ({ mount, page }) => {
  await page.route('**/api/projects/*/demo-settings', (route) => route.fulfill({ json: settings }));
  await mount(<SettingsDemos project={project} />);

  await expect(page.getByTestId(ids.demoSettingsPanel)).toBeVisible();
  await expect(page.getByTestId(ids.demoBrandPanel)).toBeVisible();
  await expect(page.getByTestId(ids.youtubePanel)).toBeVisible();
});
