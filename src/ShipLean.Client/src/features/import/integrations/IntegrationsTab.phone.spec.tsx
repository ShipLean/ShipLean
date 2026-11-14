import { expect, test } from '@playwright/experimental-ct-react';
import type { ImportJobDto } from '@shiplean/core/api/import';
import { connection, json, project, stubImport } from '../ct/fixtures';
import { importTestIds as ids } from '../testIds';
import { IntegrationsTab } from './IntegrationsTab';

// Project settings, Integrations, on a phone (SHIP-732 P7, L732c): the connection with 32px buttons, and import jobs on
// two lines. Regenerate with scripts/update-page-shots.sh src/features/import/integrations/IntegrationsTab.phone.spec.tsx.

const THEMES = ['light', 'dark'] as const;

const job: ImportJobDto = {
  id: 'j1',
  jiraProjectKey: 'PLATFORM',
  projectKey: 'SHIP',
  projectId: project.id,
  status: 'done',
  progress: {
    percent: 100,
    step: null,
    message: '',
    rerun: false,
    counts: {},
    usersMatched: 0,
    usersCreated: 0,
    statesCreated: 0,
    rerunResult: null,
  } as unknown as ImportJobDto['progress'],
  startedAt: null,
  startedLabel: '2 days ago',
  finishedAt: null,
  finishedLabel: '2 days ago',
  createdBy: null,
};

for (const theme of THEMES) {
  test(`IntegrationsPhone_WhenOpened_ShouldMatchItsScreenshot_settings-integrations-phone_${theme}`, async ({
    mount,
    page,
  }) => {
    expect(page.viewportSize()?.width, 'phone shots run only in the CT phone project').toBe(390);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubImport(page, connection);
    await page.route(`**/api/projects/${project.id}/import/jobs`, (route) =>
      json(route, [job, { ...job, id: 'j2', jiraProjectKey: 'MOBILE-APP-LONG-KEY' }]),
    );
    await mount(<IntegrationsTab project={project} />, {
      hooksConfig: { route: '/p/SHIP/settings/integrations' },
    });
    const tab = page.getByTestId(ids.integrationsTab);
    await expect(page.getByTestId(ids.jobRow)).toHaveCount(2);

    const box = async (id: string) => (await page.getByTestId(id).first().boundingBox())!;
    const panel = (await tab.boundingBox())!;
    const test = await box(ids.test);
    expect(test.height).toBeGreaterThanOrEqual(32);
    expect(test.width).toBeGreaterThan(panel.width - 80);
    const open = await box(ids.openImport);
    expect(open.height).toBeGreaterThanOrEqual(32);
    expect(open.width).toBeGreaterThan(panel.width - 80);
    const link = await box(ids.jobLink);
    expect(link.height).toBeGreaterThanOrEqual(32);
    // Two lines: the link sits under the status, not beside it.
    const row = await box(ids.jobRow);
    expect(row.height).toBeGreaterThan(50);
    expect(await tab.evaluate((el) => el.scrollWidth <= el.clientWidth)).toBe(true);

    await expect(page).toHaveScreenshot(`settings-integrations-phone.${theme}.png`, {
      animations: 'disabled',
      fullPage: true,
    });
  });
}
