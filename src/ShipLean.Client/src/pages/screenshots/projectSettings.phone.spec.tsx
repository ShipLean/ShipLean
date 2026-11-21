import { expect } from '@playwright/experimental-ct-react';
import { connection, jiraProjects } from '../../features/import/ct/fixtures';
import { importTestIds } from '../../features/import/testIds';
import { issueIntakeSample } from '../../features/settings/issueIntake/ct/issueIntakeStub';
import { settingsTestIds as ids } from '../../features/settings/testIds';
import { shellTestIds } from '../../features/shell/testIds';
import { touchTestIds } from '../../touch/testIds';
import { projectSettings } from './fixtures/workItemSettings';
import { SHELL_MEMBERS, stubShell } from './shellStub';
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
// A pane is a sub-page at touch (SHIP-1809 P2): ‹ back to the index and the pane's crumb, no tab row.
const ready = async (page: Page) => {
  await expect(page.getByTestId(touchTestIds.topBarBack)).toBeVisible();
  await expect(page.getByTestId(ids.tabRow)).toHaveCount(0);
};

phonePageShots([
  {
    name: 'project-settings-index',
    route: '/p/SHIP/settings',
    shell: true,
    arrange,
    ready: async (page) => {
      await expect(page.getByTestId(ids.settingsIndexRow)).toHaveCount(8);
      await expect(page.getByTestId(ids.settingsIndexCount)).toHaveText(
        String(SHELL_MEMBERS.length),
      );
      await expect(page.getByTestId(shellTestIds.crumb)).toHaveText(['SHIP', 'Settings']);
    },
  },
  {
    name: 'project-settings-general',
    route: '/p/SHIP/settings/general',
    shell: true,
    arrange,
    ready: async (page) => {
      await ready(page);
      await expect(page.getByTestId(shellTestIds.crumb)).toHaveText([
        'SHIP',
        'Settings',
        'General',
      ]);
    },
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
      await page.route('**/api/projects/p1/settings/issue-intake', (route) =>
        route.fulfill({ json: issueIntakeSample }),
      );
    },
    ready: async (page) => {
      await ready(page);
      await expect(page.getByTestId(importTestIds.jobsEmpty)).toBeVisible();
      await expect(page.getByTestId(importTestIds.test)).toBeVisible();
      await expect(page.getByTestId(ids.issueIntakeRepository)).toBeVisible();
    },
  },
]);
