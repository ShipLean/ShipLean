import { expect, test } from '@playwright/experimental-ct-react';
import { TaskboardSlot } from '../../slots/TaskboardSlot';
import { PROJECT, SPRINT, SPRINTS, VIEW } from '../taskboard/ct/fixtures';
import { stubTaskboard } from '../taskboard/ct/stubApi';
import { taskboardTestIds } from '../taskboard/testIds';
import { LocationProbe } from './ct/LocationProbe';
import { taskboardSettingsTestIds } from './testIds';

test('Taskboard_WhenSettingsClicked_ShouldSetTheFieldsSectionAndKeepTheQuery', async ({
  mount,
  page,
}) => {
  await stubTaskboard(page, VIEW);
  await mount(
    <>
      <TaskboardSlot project={PROJECT} sprint={SPRINT} sprints={SPRINTS} />
      <LocationProbe />
    </>,
    { hooksConfig: { route: '/p/SHIP/sprints/s24/taskboard?view=people' } },
  );

  await page.getByTestId(taskboardTestIds.settingsOpen).click();

  await expect(page.getByTestId(taskboardSettingsTestIds.locationProbe)).toHaveText(
    /settings=fields/,
  );
  await expect(page.getByTestId(taskboardSettingsTestIds.locationProbe)).toHaveText(/view=people/);
});
