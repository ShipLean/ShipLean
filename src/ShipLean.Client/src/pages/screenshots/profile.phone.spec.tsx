import { expect } from '@playwright/experimental-ct-react';
import { agentsTestIds } from '../../features/agents/testIds';
import { devicesTestIds } from '../../features/devices/testIds';
import { pagesTestIds } from '../../features/pages/testIds';
import { settingsTestIds as ids } from '../../features/settings/testIds';
import { MY_PROJECTS, stubAgents, stubDevices, stubPhoto } from './fixtures/accountStubs';
import { stubShell } from './shellStub';
import { phonePageShots } from './shootPhone';

// Your profile on a phone (SHIP-733), the phone twin of the `profile` shot in account.spec.tsx.
// Regenerate with scripts/update-page-shots.sh src/pages/screenshots/profile.phone.spec.tsx.

phonePageShots([
  {
    name: 'profile',
    route: '/profile',
    shell: false,
    arrange: async (page) => {
      await stubShell(page, {
        me: {
          name: 'Priya Shah',
          email: 'priya@larkspur.dev',
          isInstanceAdmin: true,
          avatarColour: '#2f7fd8',
          photoUrl: null,
        },
        projects: MY_PROJECTS,
        instanceName: 'Larkspur Engineering',
      });
      await stubPhoto(page);
      await stubAgents(page);
      await stubDevices(page);
    },
    ready: async (page) => {
      await expect(page.getByTestId(pagesTestIds.profilePage)).toBeVisible();
      await expect(page.getByTestId(ids.myProjectsList)).toBeVisible();
      await expect(page.getByTestId(agentsTestIds.connectedRow)).toHaveCount(1);
      await expect(page.getByTestId(devicesTestIds.row)).toHaveCount(2);
    },
  },
]);
