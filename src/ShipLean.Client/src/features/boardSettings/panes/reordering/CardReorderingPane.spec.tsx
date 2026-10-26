import { expect, test } from '@playwright/experimental-ct-react';
import type { BoardSettingsDto, BoardSettingsPut } from '../../../../api/boards';
import type { CardReordering } from '../../../../api/persisted';
import { settingsDialogTestIds as dialogIds } from '../../../settingsDialog/testIds';
import { BoardSettingsDialog } from '../../BoardSettingsDialog';
import {
  BOARD_ID,
  boardSettingsDto,
  emptySettings,
  PROJECT_ID,
} from '../../ct/boardSettingsFixtures';
import { boardSettingsTestIds as ids } from '../../testIds';

interface StubbedRoute {
  request(): { method(): string; postDataJSON(): unknown };
  fulfill(response: { json: unknown; status?: number }): Promise<void>;
}
interface Routable {
  route(url: string, handler: (route: StubbedRoute) => Promise<void>): Promise<unknown>;
}

async function stubBoard(
  page: Routable,
  saved: CardReordering | null,
): Promise<BoardSettingsPut[]> {
  const dto: BoardSettingsDto = boardSettingsDto({
    settings: { ...emptySettings(), cardReordering: saved },
  });
  const puts: BoardSettingsPut[] = [];
  await page.route(`**/api/projects/${PROJECT_ID}/boards`, (route) =>
    route.fulfill({ json: [{ id: BOARD_ID, level: 'stories', name: 'stories' }] }),
  );
  await page.route(`**/api/boards/${BOARD_ID}/settings`, async (route) => {
    if (route.request().method() === 'GET') return route.fulfill({ json: dto });
    const body = route.request().postDataJSON() as BoardSettingsPut;
    puts.push(body);
    return route.fulfill({
      json: { board: { ...dto, settings: body.settings }, mergedSections: [] },
    });
  });
  return puts;
}

const mountReordering = (mount: (component: React.JSX.Element) => Promise<unknown>) =>
  mount(
    <BoardSettingsDialog
      projectId={PROJECT_ID}
      level="stories"
      section="reordering"
      onSectionChange={() => undefined}
      onClose={() => undefined}
    />,
  );

test('CardReorderingPane_WhenChoiceChanged_ShouldPutIt', async ({ mount, page }) => {
  const puts = await stubBoard(page, 'keepsBacklogOrder');
  await mountReordering(mount);
  await expect(page.getByTestId(ids.reorderingKeepsBacklog)).toBeChecked();

  await page.getByTestId(ids.reorderingChangesBacklog).check();
  await page.getByTestId(dialogIds.save).click();

  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0].settings.cardReordering).toBe('changesBacklogOrder');
});

test('CardReorderingPane_WhenNothingSaved_ShouldShowKeepingBacklogOrder', async ({
  mount,
  page,
}) => {
  await stubBoard(page, null);
  await mountReordering(mount);

  await expect(page.getByTestId(ids.reorderingKeepsBacklog)).toBeChecked();
  await expect(page.getByTestId(ids.reorderingChangesBacklog)).not.toBeChecked();
});
