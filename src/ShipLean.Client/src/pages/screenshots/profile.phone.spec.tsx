import { expect } from '@playwright/experimental-ct-react';
import { agentsTestIds } from '../../features/agents/testIds';
import { devicesTestIds } from '../../features/devices/testIds';
import { pagesTestIds } from '../../features/pages/testIds';
import { settingsTestIds as ids } from '../../features/settings/testIds';
import { MY_PROJECTS, stubAgents, stubDevices, stubPhoto } from './fixtures/accountStubs';
import type { Page } from './shoot';
import { SHELL_ZONE, stubShell } from './shellStub';
import { phonePageShots } from './shootPhone';

// Your profile on a phone (SHIP-733), the phone twin of the `profile` shot in account.spec.tsx.
// Regenerate with scripts/update-page-shots.sh src/pages/screenshots/profile.phone.spec.tsx.

const arrange = (photo: boolean) => async (page: Page) => {
  await stubShell(page, {
    me: {
      name: 'Priya Shah',
      email: 'priya@larkspur.dev',
      isInstanceAdmin: true,
      ...SHELL_ZONE,
      avatarColour: '#2f7fd8',
      photoUrl: photo ? '/api/users/me/photo?v=1' : null,
    },
    projects: MY_PROJECTS,
    instanceName: 'Larkspur Engineering',
  });
  await stubPhoto(page);
  await stubAgents(page);
  await stubDevices(page);
};

const ready = async (page: Page) => {
  await expect(page.getByTestId(pagesTestIds.profilePage)).toBeVisible();
  await expect(page.getByTestId(ids.myProjectsList)).toBeVisible();
  await expect(page.getByTestId(agentsTestIds.connectedRow)).toHaveCount(1);
  await expect(page.getByTestId(devicesTestIds.row)).toHaveCount(2);
  // SPRINT-13: the intro line sits above the panels (Profile_ShouldShowTheIntroLine).
  await expect(page.getByTestId('profile-intro')).toBeVisible();
};

// Scrolls a panel to just under the top bar.
const scrollToTop = (page: Page, testId: string) =>
  page.getByTestId(testId).evaluate((el) => {
    el.style.scrollMarginTop = '64px';
    el.scrollIntoView({ block: 'start' });
  });

phonePageShots([
  { name: 'profile', route: '/profile', shell: false, arrange: arrange(false), ready },
  {
    name: 'profile-password',
    route: '/profile',
    shell: false,
    arrange: arrange(false),
    ready,
    act: async (page) => {
      await scrollToTop(page, ids.passwordPanel);
      await expect(page.getByTestId(ids.passwordPanel)).toBeInViewport();
      await expect(page.getByTestId(ids.passwordForm)).toHaveCount(0);
    },
  },
  {
    name: 'profile-photo',
    route: '/profile',
    shell: false,
    arrange: arrange(true),
    ready,
    act: async (page) => {
      await expect(page.getByTestId(ids.profileAvatarPhoto).first()).toBeVisible();
      await expect(page.getByTestId(ids.profilePhotoRemove)).toBeVisible();
    },
  },
  {
    name: 'profile-devices',
    route: '/profile',
    shell: false,
    arrange: arrange(false),
    ready,
    act: async (page) => {
      await scrollToTop(page, devicesTestIds.panel);
      await expect(page.getByTestId(devicesTestIds.panel)).toBeInViewport();
    },
  },
]);
