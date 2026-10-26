import { expect, test } from '@playwright/experimental-ct-react';
import type { BoardSettingsPut } from '../../api/boards';
import { TaskboardSettingsSlot } from '../../slots/TaskboardSettingsSlot';
import { TaskboardSlot } from '../../slots/TaskboardSlot';
import {
  boardSettingsDto,
  emptySettings,
  TASKBOARD_ID,
} from '../boardSettings/ct/boardSettingsFixtures';
import { PROJECT, SPRINT, SPRINTS, VIEW } from '../taskboard/ct/fixtures';
import { stubTaskboard } from '../taskboard/ct/stubApi';
import { taskboardTestIds } from '../taskboard/testIds';
import { taskboardSettingsTestIds as ids } from './testIds';

const route = { hooksConfig: { route: '/p/SHIP/sprints/s24/taskboard' } };

test('TaskboardSettingsSlot_WhenFieldToggled_ShouldRefreshTheOpenTaskboard', async ({
  mount,
  page,
}) => {
  const server = await stubTaskboard(page, VIEW);
  const dto = boardSettingsDto({
    id: TASKBOARD_ID,
    projectId: PROJECT.id,
    level: 'taskboard',
    settings: {
      ...emptySettings(),
      cardFields: [
        {
          type: 'task',
          showKey: true,
          showAssignee: true,
          showTags: true,
          showPoints: false,
          showEmptyFields: false,
          extraFields: [],
        },
      ],
    },
  });
  const puts: BoardSettingsPut[] = [];
  await page.route(`**/api/projects/${PROJECT.id}/boards`, (r) =>
    r.fulfill({ json: [{ id: TASKBOARD_ID, level: 'taskboard', name: 'Taskboard' }] }),
  );
  await page.route(`**/api/boards/${TASKBOARD_ID}/settings`, async (r) => {
    if (r.request().method() === 'GET') return r.fulfill({ status: 200, json: dto });
    const body = r.request().postDataJSON() as BoardSettingsPut;
    puts.push(body);
    return r.fulfill({ json: { board: { ...dto, settings: body.settings }, mergedSections: [] } });
  });
  await mount(
    <>
      <TaskboardSlot project={PROJECT} sprint={SPRINT} sprints={SPRINTS} />
      <TaskboardSettingsSlot
        project={PROJECT}
        sprint={SPRINT}
        sprints={SPRINTS}
        section="fields"
        onSectionChange={() => undefined}
        onClose={() => undefined}
      />
    </>,
    route,
  );
  await expect(page.getByTestId(taskboardTestIds.grid)).toBeVisible();
  await expect(page.getByTestId(ids.fieldsPane)).toBeVisible();
  // Opening the dialog (a plain fetch of the settings) does not touch the board.
  expect(server.reads).toBe(1);

  await page.getByTestId(ids.fieldsShowPoints).check();

  await expect.poll(() => puts.length).toBe(1);
  await expect.poll(() => server.reads).toBe(2);
});

test('TaskboardSettingsSlot_WhenNoSection_ShouldShowNoDialog', async ({ mount, page }) => {
  await mount(
    <TaskboardSettingsSlot
      project={PROJECT}
      sprint={SPRINT}
      sprints={SPRINTS}
      section={undefined}
      onSectionChange={() => undefined}
      onClose={() => undefined}
    />,
    route,
  );
  await expect(page.getByTestId(ids.fieldsPane)).toHaveCount(0);
});
