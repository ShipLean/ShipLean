import { expect } from '@playwright/experimental-ct-react';
import { stubBoard } from '../../features/board/ct/stubApi';
import { boardSettingsTestIds as ids } from '../../features/boardSettings/testIds';
import { settingsDialogTestIds } from '../../features/settingsDialog/testIds';
import { touchTestIds } from '../../touch/testIds';
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
  {
    ...pane('board-settings-styles-dirty', 'styles', (page) =>
      expect(page.getByTestId(ids.styleRow)).toHaveCount(3),
    ),
    act: async (page: Page) => {
      await page.getByTestId(ids.styleEnabled).nth(1).tap();
      await expect(page.getByTestId(settingsDialogTestIds.footerDirty)).toHaveText(
        'Unsaved: Styles',
      );
    },
  },
  {
    ...pane('board-settings-styles-drag', 'styles', (page) =>
      expect(page.getByTestId(ids.styleRow)).toHaveCount(3),
    ),
    // Hold the first rule's grip for the K5 lift, then move halfway down the second rule and keep holding.
    act: async (page: Page) => {
      const grip = await page.getByTestId(ids.styleGrip).first().boundingBox();
      const over = await page.getByTestId(ids.styleRow).nth(1).boundingBox();
      if (!grip || !over) throw new Error('no box');
      const from = {
        x: Math.round(grip.x + grip.width / 2),
        y: Math.round(grip.y + grip.height / 2),
      };
      const to = {
        x: Math.round(over.x + over.width / 2),
        y: Math.round(over.y + over.height / 2),
      };
      await page.clock.install({ time: 0 });
      await page.clock.pauseAt(1000);
      const cdp = await page.context().newCDPSession(page);
      await cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [from] });
      await page.clock.runFor(450);
      await expect(page.getByTestId(touchTestIds.dragGhost)).toBeVisible();
      for (let i = 1; i <= 10; i++) {
        await cdp.send('Input.dispatchTouchEvent', {
          type: 'touchMove',
          touchPoints: [
            { x: from.x + ((to.x - from.x) * i) / 10, y: from.y + ((to.y - from.y) * i) / 10 },
          ],
        });
      }
      await expect(page.getByTestId(touchTestIds.dragGhost)).toBeVisible();
    },
  },
  pane('board-settings-columns', 'columns', (page) =>
    expect(page.getByTestId(ids.columnRow)).toHaveCount(5),
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
