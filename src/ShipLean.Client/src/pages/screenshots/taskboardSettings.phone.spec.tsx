import type { Page } from 'playwright';
import { expect } from '@playwright/experimental-ct-react';
import { stubSettings, taskboardBoard } from '../../features/taskboardSettings/ct/stubSettings';
import { taskboardSettingsTestIds } from '../../features/taskboardSettings/testIds';
import { stubTaskboard } from '../../features/taskboard/ct/stubApi';
import {
  TASKBOARD_CARD_FIELDS,
  TASKBOARD_STYLE_RULES,
  TASKBOARD_VIEW,
} from './fixtures/taskboardView';
import { phonePageShots } from './shootPhone';
import { stubShell, SHELL_MEMBERS, SHELL_PROJECT, SHELL_SPRINTS } from './shellStub';

// Taskboard settings full screen on a phone (SHIP-725, L725d). Regenerate with
// scripts/update-page-shots.sh src/pages/screenshots/taskboardSettings.phone.spec.tsx.

phonePageShots([
  {
    name: 'taskboard-settings',
    route: '/p/SHIP/sprints/sprint-24/taskboard?settings=fields',
    shell: true,
    arrange: async (page: Page) => {
      await stubShell(page);
      await stubTaskboard(page, TASKBOARD_VIEW, {});
      await page.route('**/api/me/preferences/recentProjects', (r) => r.fulfill({ status: 204 }));
      await stubSettings(
        page,
        taskboardBoard({ styleRules: TASKBOARD_STYLE_RULES, cardFields: TASKBOARD_CARD_FIELDS }),
        ['Blocked', 'Spike', 'Tech debt'],
        SHELL_PROJECT.id,
      );
      // The settings stub answers empty sprints and members; the shell's own must win again.
      await page.route(`**/api/projects/${SHELL_PROJECT.id}/sprints`, (r) =>
        r.fulfill({ json: SHELL_SPRINTS }),
      );
      await page.route(`**/api/projects/${SHELL_PROJECT.id}/members`, (r) =>
        r.fulfill({ json: SHELL_MEMBERS }),
      );
    },
    ready: (page: Page) =>
      expect(page.getByTestId(taskboardSettingsTestIds.fieldsPane)).toBeVisible(),
  },
]);
