import { expect } from '@playwright/experimental-ct-react';
import { stubBoard } from '../../features/board/ct/stubApi';
import { boardSettingsTestIds as ids } from '../../features/boardSettings/testIds';
import { BOARD_ID, storiesBoardForSettings, storiesSettings } from './fixtures/boards';
import { stubShell } from './shellStub';
import { phonePageShots } from './shootPhone';
import type { Page } from './shoot';

// Stories board settings full screen on a phone (SHIP-732; phone design settings-board-styles*, SHIP-1809 P9). Regenerate
// with scripts/update-page-shots.sh src/pages/screenshots/boardSettings.phone.spec.tsx.

async function arrange(page: Page) {
  await stubShell(page);
  await stubBoard(page, storiesBoardForSettings(), { members: [] });
  await page.route('**/api/projects/p1/tags', (route) =>
    route.fulfill({ json: ['Blocked', 'Spike'] }),
  );
  await page.route('**/api/markdown/render', (route) => {
    const { markdown } = route.request().postDataJSON() as { markdown: string };
    const items = markdown.split('\n').map((line) => `<li>${line.replace(/^- /, '')}</li>`);
    return route.fulfill({ json: { markdown, html: `<ul>${items.join('')}</ul>` } });
  });
  await page.route(`**/api/boards/${BOARD_ID}/settings`, (route) =>
    route.fulfill({ json: storiesSettings() }),
  );
}

const pane = (name: string, settings: string, ready: (page: Page) => Promise<void>) => ({
  name,
  route: `/p/SHIP/boards/stories?settings=${settings}`,
  shell: true,
  arrange,
  ready,
});

phonePageShots([
  pane('board-settings-styles', 'styles', (page) =>
    expect(page.getByTestId(ids.styleRow)).toHaveCount(3),
  ),
  pane('board-settings-columns', 'columns', (page) =>
    expect(page.getByTestId(ids.columnRow)).toHaveCount(5),
  ),
  pane('board-settings-fields', 'fields', (page) =>
    expect(page.getByTestId(ids.fieldsPane)).toBeVisible(),
  ),
  pane('board-settings-tags', 'tags', (page) =>
    expect(page.getByTestId(ids.tagRow).first()).toBeVisible(),
  ),
  pane('board-settings-annotations', 'annotations', (page) =>
    expect(page.getByTestId(ids.annotationsPane)).toBeVisible(),
  ),
  pane('board-settings-swimlanes', 'swimlanes', (page) =>
    expect(page.getByTestId(ids.swimlanesPane)).toBeVisible(),
  ),
  pane('board-settings-automations', 'automations', (page) =>
    expect(page.getByTestId(ids.automationsPane)).toBeVisible(),
  ),
]);
