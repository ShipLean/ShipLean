import { expect, test } from '@playwright/experimental-ct-react';
import { componentTestIds } from '../../components/testIds';
import { settingsDialogTestIds as ids } from '../settingsDialog/testIds';
import {
  boardSettingsDto,
  PROJECT_ID,
  TASKBOARD_ID,
} from '../boardSettings/ct/boardSettingsFixtures';
import { TaskboardSettingsDialog } from './TaskboardSettingsDialog';
import { taskboardSettingsTestIds } from './testIds';

test('TaskboardSettingsDialog_WhenOpened_ShouldFindTheTaskboardBoardAndDisableColumns', async ({
  mount,
  page,
}) => {
  await page.route(`**/api/projects/${PROJECT_ID}/boards`, (route) =>
    route.fulfill({
      json: [
        { id: 'other', level: 'stories', name: 'Stories' },
        { id: TASKBOARD_ID, level: 'taskboard', name: 'Taskboard' },
      ],
    }),
  );
  await page.route(`**/api/boards/${TASKBOARD_ID}/settings`, (route) =>
    route.fulfill({ json: boardSettingsDto({ id: TASKBOARD_ID, level: 'taskboard' }) }),
  );
  await mount(
    <TaskboardSettingsDialog
      projectId={PROJECT_ID}
      section="styles"
      onSectionChange={() => undefined}
      onClose={() => undefined}
    />,
  );
  await expect(page.getByTestId(taskboardSettingsTestIds.stylesPane)).toBeAttached();
  await expect(page.getByTestId(`${ids.navItem}-columns`)).toBeDisabled();
  await expect(page.getByTestId(`${ids.navDisabledHint}-columns`)).toContainText('Task states');
  await expect(page.getByTestId(ids.save)).toHaveCount(0);
  await expect(page.getByTestId(componentTestIds.dialog)).toHaveCSS('width', '880px');
  await expect(page.getByTestId(ids.immediateNote)).toBeVisible();
  // the disabled Columns item is described by the hint
  await expect(page.getByTestId(`${ids.navItem}-columns`)).toHaveAccessibleDescription(
    /Task states/,
  );
});

test('TaskboardSettingsDialog_WhenNoTaskboardBoard_ShouldSayItFailed', async ({ mount, page }) => {
  await page.route(`**/api/projects/${PROJECT_ID}/boards`, (route) => route.fulfill({ json: [] }));
  await mount(
    <TaskboardSettingsDialog
      projectId={PROJECT_ID}
      section={undefined}
      onSectionChange={() => undefined}
      onClose={() => undefined}
    />,
  );
  await expect(page.getByTestId(taskboardSettingsTestIds.loadFailed)).toBeVisible();
});
