import { expect } from '@playwright/experimental-ct-react';
import type { Locator } from 'playwright';
import type { SwimlaneJson } from '@shiplean/core/api/persisted';
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

// Three lanes as the Features board design draws them: a criteria lane, a manual lane and the default lane.
const lanes: SwimlaneJson[] = [
  {
    id: 'lane-expedite',
    name: 'Expedite',
    colour: 'warn',
    kind: 'criteria',
    criteria: { match: 'all', clauses: [{ field: 'priority', op: '=', value: 1 }] },
  },
  { id: 'lane-debt', name: 'Tech debt', colour: null, kind: 'manual', criteria: null },
  { id: 'lane-default', name: 'Everything else', colour: null, kind: 'default', criteria: null },
];

// Hold `grip` for the K5 lift (450ms on a paused clock), move to the middle of `over` in ten steps and keep holding.
async function holdAndDrag(page: Page, grip: Locator, over: Locator) {
  const from = await grip.boundingBox();
  const to = await over.boundingBox();
  if (!from || !to) throw new Error('no box');
  const start = {
    x: Math.round(from.x + from.width / 2),
    y: Math.round(from.y + from.height / 2),
  };
  const end = { x: Math.round(to.x + to.width / 2), y: Math.round(to.y + to.height / 2) };
  await page.clock.install({ time: 0 });
  await page.clock.pauseAt(1000);
  const cdp = await page.context().newCDPSession(page);
  await cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [start] });
  await page.clock.runFor(450);
  await expect(page.getByTestId(touchTestIds.dragGhost)).toBeVisible();
  for (let i = 1; i <= 10; i++) {
    await cdp.send('Input.dispatchTouchEvent', {
      type: 'touchMove',
      touchPoints: [
        { x: start.x + ((end.x - start.x) * i) / 10, y: start.y + ((end.y - start.y) * i) / 10 },
      ],
    });
  }
  await expect(page.getByTestId(touchTestIds.dragGhost)).toBeVisible();
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
    act: (page: Page) =>
      holdAndDrag(
        page,
        page.getByTestId(ids.styleGrip).first(),
        page.getByTestId(ids.styleRow).nth(1),
      ),
  },
  pane('board-settings-columns', 'columns', (page) =>
    expect(page.getByTestId(ids.columnRow)).toHaveCount(5),
  ),
  {
    ...pane('board-settings-columns-drag', 'columns', (page) =>
      expect(page.getByTestId(ids.columnRow)).toHaveCount(5),
    ),
    // Hold Ready's grip for the K5 lift, then move onto In review's name and keep holding (SHIP-1985).
    act: (page: Page) =>
      holdAndDrag(
        page,
        page.getByTestId(ids.columnGrip).nth(1),
        page.getByTestId(ids.columnName).nth(3),
      ),
  },
  pane('board-settings-tags', 'tags', (page) =>
    expect(page.getByTestId(ids.tagRow).first()).toBeVisible(),
  ),
  pane('board-settings-annotations', 'annotations', (page) =>
    expect(page.getByTestId(ids.annotationsPane)).toBeVisible(),
  ),
  pane('board-settings-swimlanes', 'swimlanes', (page) =>
    expect(page.getByTestId(ids.swimlanesPane)).toBeVisible(),
  ),
  {
    ...pane('board-settings-swimlanes-lanes', 'swimlanes', (page) =>
      expect(page.getByTestId(ids.laneRow)).toHaveCount(3),
    ),
    arrange: async (page: Page) => {
      await arrange(page);
      await page.route(`**/api/boards/${BOARD_ID}/settings`, (route) =>
        route.fulfill({
          json: {
            ...storiesSettings(),
            settings: { ...storiesSettings().settings, swimlanes: lanes },
          },
        }),
      );
    },
  },
  pane('board-settings-automations', 'automations', (page) =>
    expect(page.getByTestId(ids.automationsPane)).toBeVisible(),
  ),
]);
