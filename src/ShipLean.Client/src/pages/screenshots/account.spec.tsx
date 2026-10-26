import { expect } from '@playwright/experimental-ct-react';
import { meDto } from '../../features/auth/authFixtures';
import { authTestIds as auth } from '../../features/auth/testIds';
import { agentsTestIds } from '../../features/agents/testIds';
import { pagesTestIds } from '../../features/pages/testIds';
import { settingsTestIds as ids } from '../../features/settings/testIds';
import { appTestIds } from '../../testIds';
import {
  MY_PROJECTS,
  stubAdminUsers,
  stubAgents,
  stubConsent,
  stubInstanceProjects,
  stubInstanceSettings,
  stubNewProject,
  stubPhoto,
} from './fixtures/accountStubs';
import { stubShell } from './shellStub';
import { pageShots, type Page } from './shoot';

const priyaAdmin = {
  name: 'Priya Shah',
  email: 'priya@larkspur.dev',
  isInstanceAdmin: true,
  avatarColour: '#2f7fd8',
};

const instanceName = 'Larkspur Engineering';

async function stubSignIn(page: Page, status: 'active' | 'oneTimePassword', email: string) {
  await page.route('**/api/auth/instance', (route) =>
    route.fulfill({ json: { name: instanceName } }),
  );
  await page.route('**/api/auth/login', (route) =>
    status === 'active'
      ? route.fulfill({
          status: 429,
          json: {
            error:
              "That email and password didn't work. Check them and try again, or ask your ShipLean admin.",
            code: 'rate_limited',
          },
        })
      : route.fulfill({ json: meDto({ status, email }) }),
  );
}

async function signIn(page: Page, email: string) {
  await page.getByTestId(auth.email).fill(email);
  await page.getByTestId(auth.password).fill('a-password');
  await page.getByTestId(auth.signInSubmit).click();
}

const profileArrange = async (page: Page, photo: boolean) => {
  await stubShell(page, {
    me: { ...priyaAdmin, photoUrl: photo ? '/api/users/me/photo?v=1' : null },
    projects: MY_PROJECTS,
    instanceName,
  });
  await stubPhoto(page);
  await stubAgents(page);
};

const profileReady = async (page: Page) => {
  await expect(page.getByTestId(pagesTestIds.profilePage)).toBeVisible();
  await expect(page.getByTestId(ids.myProjectsList)).toBeVisible();
  await expect(page.getByTestId(agentsTestIds.connectedRow)).toHaveCount(1);
};

const instanceArrange = async (page: Page) => {
  await stubShell(page, { me: priyaAdmin, projects: MY_PROJECTS, instanceName });
  await stubInstanceSettings(page);
  await stubInstanceProjects(page);
  await stubAdminUsers(page);
};

pageShots([
  {
    name: 'signin',
    route: '/login',
    shell: false,
    arrange: async (page) => {
      await stubSignIn(page, 'active', 'priya@larkspur.dev');
    },
    ready: (page) => expect(page.getByTestId(appTestIds.loginPage)).toBeVisible(),
  },
  {
    name: 'signin-error',
    route: '/login',
    shell: false,
    arrange: async (page) => {
      await stubSignIn(page, 'active', 'priya@larkspur.dev');
    },
    ready: (page) => expect(page.getByTestId(appTestIds.loginPage)).toBeVisible(),
    act: async (page) => {
      await signIn(page, 'priya@larkspur.dev');
      await expect(page.getByTestId(auth.failure)).toBeVisible();
      await expect(page.getByTestId(auth.password)).toBeFocused();
    },
  },
  {
    name: 'signin-change',
    route: '/login',
    shell: false,
    arrange: async (page) => {
      await stubSignIn(page, 'oneTimePassword', 'ravi@larkspur.dev');
    },
    ready: (page) => expect(page.getByTestId(appTestIds.loginPage)).toBeVisible(),
    act: async (page) => {
      await signIn(page, 'ravi@larkspur.dev');
      await expect(page.getByTestId(auth.changeForm)).toBeVisible();
      await expect(page.getByTestId(auth.newPassword)).toBeFocused();
    },
  },
  {
    name: 'signin-no-projects',
    route: '/login?state=no-projects',
    shell: false,
    arrange: async (page) => {
      await stubSignIn(page, 'active', 'priya@larkspur.dev');
      await page.route('**/api/me', (route) =>
        route.fulfill({ json: meDto({ ...priyaAdmin, hasProjects: false }) }),
      );
    },
    ready: (page) => expect(page.getByTestId(auth.noProjects)).toBeVisible(),
  },
  {
    name: 'new-project',
    route: '/projects/new',
    shell: false,
    arrange: async (page) => {
      await stubShell(page, { me: priyaAdmin, projects: MY_PROJECTS, instanceName });
      await stubNewProject(page);
    },
    ready: (page) => expect(page.getByTestId(ids.newProjectForm)).toBeVisible(),
    act: async (page) => {
      await page.getByTestId(ids.newProjectName).fill('Customer Support Hub');
      await page.getByTestId(ids.newProjectMemberEmail).fill('mei@larkspur.dev');
      await page.getByTestId(ids.newProjectMemberAdd).click();
      await expect(page.getByTestId(ids.newProjectMemberRow)).toHaveCount(1);
      await expect(page.getByTestId(ids.newProjectKey)).toHaveValue('CSH');
      await expect(page.getByTestId(ids.newProjectCheckFree)).toHaveAttribute('data-ok', 'true');
      await expect(page.getByTestId(ids.newProjectAlternative)).toHaveCount(3);
    },
  },
  {
    name: 'instance',
    route: '/instance/users',
    shell: false,
    arrange: instanceArrange,
    ready: (page) => expect(page.getByTestId(ids.usersRow)).toHaveCount(9),
  },
  {
    name: 'instance-general',
    route: '/instance/general',
    shell: false,
    arrange: instanceArrange,
    ready: (page) => expect(page.getByTestId(ids.instanceGeneralPanel)).toBeVisible(),
    act: async (page) => {
      await expect(page.getByTestId(ids.instanceName)).toHaveValue(instanceName);
    },
  },
  {
    name: 'instance-projects',
    route: '/instance/projects',
    shell: false,
    arrange: instanceArrange,
    ready: (page) => expect(page.getByTestId(ids.projectsRow)).toHaveCount(8),
  },
  {
    name: 'profile',
    route: '/profile',
    shell: false,
    arrange: (page) => profileArrange(page, false),
    ready: profileReady,
  },
  {
    name: 'profile-photo',
    route: '/profile',
    shell: false,
    arrange: (page) => profileArrange(page, true),
    ready: profileReady,
    act: (page) => expect(page.getByTestId(ids.profileAvatarPhoto).first()).toBeVisible(),
  },
  {
    name: 'authorize',
    route: '/authorize?request=r-1',
    shell: false,
    arrange: async (page) => {
      await stubShell(page, { me: priyaAdmin, projects: MY_PROJECTS, instanceName });
      await stubConsent(page);
    },
    ready: (page) => expect(page.getByTestId(agentsTestIds.consentCard)).toBeVisible(),
  },
]);
