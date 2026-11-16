import { expect } from '@playwright/experimental-ct-react';
import { connection, jiraProjects } from '../../features/import/ct/fixtures';
import { importTestIds } from '../../features/import/testIds';
import { settingsTestIds as ids } from '../../features/settings/testIds';
import { touchTestIds } from '../../touch/testIds';
import { projectSettings } from './fixtures/workItemSettings';
import { stubShell } from './shellStub';
import { phonePageShots } from './shootPhone';
import type { Page } from './shoot';

// Project settings on a phone (SHIP-732, L732a): the whole page with the shell, on the tabs this task owns (General,
// Members, Sprints, Integrations) and General with an unsaved edit (the SaveBar). Regenerate with
// scripts/update-page-shots.sh src/pages/screenshots/projectSettings.phone.spec.tsx.

const arrange = async (page: Page) => {
  await stubShell(page);
  await page.route('**/api/holidays/countries', (route) =>
    route.fulfill({ json: [{ code: 'AU', name: 'Australia' }] }),
  );
  await page.route('**/api/projects/p1/settings', (route) =>
    route.fulfill({ json: projectSettings }),
  );
};
const ready = async (page: Page) => {
  await expect(page.getByTestId(ids.tabRow)).toBeVisible();
};

phonePageShots([
  {
    name: 'project-settings-general',
    route: '/p/SHIP/settings/general',
    shell: true,
    arrange,
    ready,
  },
  {
    name: 'project-settings-general-dirty',
    route: '/p/SHIP/settings/general',
    shell: true,
    arrange,
    ready,
    act: async (page) => {
      await page.getByTestId(ids.generalName).fill('Shop');
      await expect(page.getByTestId(touchTestIds.saveBar)).toBeVisible();
    },
  },
  {
    name: 'project-settings-members',
    route: '/p/SHIP/settings/members',
    shell: true,
    arrange,
    ready: async (page) => {
      await ready(page);
      await expect(page.getByTestId(ids.membersPanel)).toBeVisible();
    },
  },
  {
    name: 'project-settings-sprints',
    route: '/p/SHIP/settings/sprints',
    shell: true,
    arrange,
    ready: async (page) => {
      await ready(page);
      await expect(page.getByTestId(ids.sprintsPanel)).toBeVisible();
    },
  },
  {
    name: 'project-settings-integrations',
    route: '/p/SHIP/settings/integrations',
    shell: true,
    arrange: async (page) => {
      await arrange(page);
      await page.route('**/api/projects/p1/jira/connection', (route) =>
        route.fulfill({ json: connection }),
      );
      await page.route('**/api/projects/p1/jira/projects', (route) =>
        route.fulfill({ json: jiraProjects }),
      );
      await page.route('**/api/projects/p1/import/jobs', (route) => route.fulfill({ json: [] }));
    },
    ready: async (page) => {
      await ready(page);
      await expect(page.getByTestId(importTestIds.jobsEmpty)).toBeVisible();
      await expect(page.getByTestId(importTestIds.test)).toBeVisible();
    },
  },
]);
