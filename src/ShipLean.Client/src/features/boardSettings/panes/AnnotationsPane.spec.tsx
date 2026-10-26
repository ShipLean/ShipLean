import { expect, test } from '@playwright/experimental-ct-react';
import type { BoardLevel, BoardSettingsDto, BoardSettingsPut } from '../../../api/boards';
import { settingsDialogTestIds as dialogIds } from '../../settingsDialog/testIds';
import { BoardSettingsDialog } from '../BoardSettingsDialog';
import { BOARD_ID, boardSettingsDto, emptySettings, PROJECT_ID } from '../ct/boardSettingsFixtures';
import { boardSettingsTestIds as ids } from '../testIds';

const dto = (
  level: BoardLevel,
  annotations: Partial<BoardSettingsDto['settings']['annotations']>,
) =>
  boardSettingsDto({
    level,
    settings: {
      ...emptySettings(),
      annotations: { tasks: false, stories: false, bugs: false, features: false, ...annotations },
    },
  });

async function open(
  mount: (component: React.JSX.Element) => Promise<unknown>,
  page: {
    route(
      url: string,
      handler: (route: {
        request(): { method(): string; postDataJSON(): unknown };
        fulfill(response: { json: unknown }): Promise<void>;
      }) => Promise<void>,
    ): Promise<unknown>;
  },
  board: BoardSettingsDto,
): Promise<BoardSettingsPut[]> {
  const puts: BoardSettingsPut[] = [];
  await page.route(`**/api/projects/${PROJECT_ID}/boards`, (route) =>
    route.fulfill({ json: [{ id: BOARD_ID, level: board.level, name: board.level }] }),
  );
  await page.route(`**/api/boards/${BOARD_ID}/settings`, async (route) => {
    if (route.request().method() === 'GET') return route.fulfill({ json: board });
    const body = route.request().postDataJSON() as BoardSettingsPut;
    puts.push(body);
    return route.fulfill({
      json: { board: { ...board, settings: body.settings }, mergedSections: [] },
    });
  });
  await mount(
    <BoardSettingsDialog
      projectId={PROJECT_ID}
      level={board.level as 'stories' | 'features' | 'epics'}
      section="annotations"
      onSectionChange={() => undefined}
      onClose={() => undefined}
    />,
  );
  return puts;
}

test('AnnotationsPane_WhenEpicsBoardHasStoriesAndBugsOn_ShouldShowThemChecked', async ({
  mount,
  page,
}) => {
  await open(mount, page, dto('epics', { stories: true, bugs: true }));

  await expect(page.getByTestId(ids.annotationStories)).toBeChecked();
  await expect(page.getByTestId(ids.annotationBugs)).toBeChecked();
  await expect(page.getByTestId(ids.annotationsPreviewRollup)).toHaveCount(2);
});

test('AnnotationsPane_WhenFeaturesBoard_ShouldOfferNestedFeatures', async ({ mount, page }) => {
  await open(mount, page, dto('features', { features: true }));

  await expect(page.getByTestId(ids.annotationFeatures)).toBeChecked();
  await expect(page.getByTestId(ids.annotationsPreviewRollup)).toContainText('features');
});

test('AnnotationsPane_WhenStoriesBoard_ShouldNotOfferFeatures', async ({ mount, page }) => {
  await open(mount, page, dto('stories', { tasks: true }));

  await expect(page.getByTestId(ids.annotationTasks)).toBeChecked();
  await expect(page.getByTestId(ids.annotationBugs)).toBeDisabled();
  await expect(page.getByTestId(ids.annotationFeatures)).toHaveCount(0);
});

test('AnnotationsPane_WhenEpicsBoard_ShouldOfferFeaturesStoriesAndBugs', async ({
  mount,
  page,
}) => {
  const puts = await open(mount, page, dto('epics', {}));
  await expect(page.getByTestId(ids.annotationFeatures)).toBeEnabled();
  await expect(page.getByTestId(ids.annotationStories)).toBeEnabled();
  await expect(page.getByTestId(ids.annotationBugs)).toBeEnabled();
  await expect(page.getByTestId(ids.annotationTasks)).toBeDisabled();
  await expect(page.getByTestId(ids.annotationsPreviewRollup)).toHaveCount(0);

  await page.getByTestId(ids.annotationFeatures).check();

  await expect(page.getByTestId(ids.annotationsPreviewRollup)).toContainText('features');
  await page.getByTestId(dialogIds.save).click();
  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0].settings.annotations.features).toBe(true);
});

test('AnnotationsPane_WhenToggledOff_ShouldRemoveRollupFromPreview', async ({ mount, page }) => {
  await open(mount, page, dto('features', { stories: true, bugs: true }));
  await expect(page.getByTestId(ids.annotationsPreviewRollup)).toHaveCount(2);

  await page.getByTestId(ids.annotationBugs).uncheck();

  await expect(page.getByTestId(ids.annotationsPreviewRollup)).toHaveCount(1);
  await expect(page.getByTestId(ids.annotationsPreviewRollup)).toContainText('stories');
});
