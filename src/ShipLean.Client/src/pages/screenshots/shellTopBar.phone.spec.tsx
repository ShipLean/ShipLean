import { expect } from '@playwright/experimental-ct-react';
import searchView from '../../../../ShipLean.TestSupport/DtoFixtures/search/search-view.json' with { type: 'json' };
import { meDto } from '../../features/auth/authFixtures';
import { backlogView, ROWS } from '../../features/backlog/ct/fixtures';
import { stubBacklog } from '../../features/backlog/ct/stubApi';
import { backlogTestIds } from '../../features/backlog/testIds';
import { quickSearchTestIds } from '../../features/quickSearch/testIds';
import { realtimeTestIds } from '../../features/realtime/testIds';
import { touchTestIds } from '../../touch/testIds';
import { PEOPLE, SHELL_MEMBERS, SHELL_PROJECT, stubShell } from './shellStub';
import type { Page } from './shoot';
import { phonePageShots } from './shootPhone';

// The touch top bar on a phone (SHIP-713): the breadcrumb, the presence count and the Live dot, the bar while the
// connection is down (Reconnecting keeps its word, with the banner), and quick search full screen, empty and with
// results. Regenerate with scripts/update-page-shots.sh src/pages/screenshots/shellTopBar.phone.spec.tsx.

const ROUTE = '/p/SHIP/backlogs/stories';

// SignalR frames end with the record separator.
const RS = '\u001e';

const recent = ['SHIP-12', 'SHIP-31', 'SHIP-48'].map((key, at) => ({
  key,
  title: ['Export a board to CSV', 'Fix the nightly job', 'Quarterly numbers'][at],
  type: ['story', 'bug', 'feature'][at],
  state: ['Active', 'New', 'Closed'][at],
  stateCategory: ['inProgress', 'proposed', 'completed'][at],
  parentPath: [],
  projectKey: 'SHIP',
}));

async function arrange(page: Page) {
  await stubShell(page);
  await stubBacklog(page, backlogView(ROWS));
  // The backlog stub answers members with none; the presence count lists only known members, so the shell's win.
  await page.route('**/api/projects/p1/members', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(SHELL_MEMBERS),
    }),
  );
  await page.route('**/api/search*', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(searchView),
    }),
  );
}

interface Hub {
  /** Waits for the page to open the hub, then sends one project event. */
  push: (event: object) => Promise<void>;
  /** Closes the open socket and refuses every later negotiate, so the page stays Reconnecting. */
  drop: () => Promise<void>;
}

// Stands in for the project hub (docs/events.md): answers the negotiate, the handshake and every hub call, so the page
// goes Live. Socket routing is an init script that reaches only documents loaded after it, so this reloads the component
// page; call it before mount.
async function stubHub(page: Page): Promise<Hub> {
  let refuse = false;
  let open: () => void = () => undefined;
  const opened = new Promise<void>((resolve) => {
    open = resolve;
  });
  let socket: { send(message: string): void; close(): Promise<void> } | undefined;
  await page.route(
    (url) => url.pathname === '/hubs/project/negotiate',
    (route) =>
      refuse
        ? route.fulfill({ status: 503, body: 'down' })
        : route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
              negotiateVersion: 1,
              connectionId: 'hub-1',
              connectionToken: 'hub-token-1',
              availableTransports: [
                { transport: 'WebSockets', transferFormats: ['Text', 'Binary'] },
              ],
            }),
          }),
  );
  await page.routeWebSocket(/\/hubs\/project(\?|$)/, (ws) => {
    socket = ws;
    open();
    ws.onMessage((message) => {
      for (const frame of String(message).split(RS).filter(Boolean)) {
        const parsed = JSON.parse(frame) as {
          type?: number;
          protocol?: string;
          invocationId?: string;
        };
        if (parsed.protocol !== undefined) ws.send(`{}${RS}`);
        else if (parsed.type === 1 && parsed.invocationId !== undefined) {
          ws.send(`${JSON.stringify({ type: 3, invocationId: parsed.invocationId })}${RS}`);
        }
      }
    });
  });
  await page.reload();
  return {
    push: async (event) => {
      await opened;
      socket?.send(
        `${JSON.stringify({ type: 1, target: 'projectEvent', arguments: [event] })}${RS}`,
      );
    },
    drop: async () => {
      await opened;
      refuse = true;
      await socket?.close();
    },
  };
}

// Set by each hub shot's arrange, used by its act (each test runs its own arrange first).
let hub: Hub | undefined;

const liveDot = (page: Page) => page.getByTestId(realtimeTestIds.liveDot);

// A key pressed before the shell has loaded can beat its listener, so the shots wait for the backlog rows first.
const rowsReady = (page: Page) =>
  expect(page.getByTestId(backlogTestIds.row)).toHaveCount(ROWS.length);

// Quick search covers the bar, so the shell's mask over the Live dot would paint over Cancel: these shots wait for
// Offline (CT has no hub) themselves and leave the dot unmasked under the dialog.
async function rowsReadyOffline(page: Page) {
  await rowsReady(page);
  await expect(liveDot(page)).toHaveAttribute('data-state', 'offline');
}

// Me and three others on the stories backlog: the bar shows +3.
async function liveWithThreeOthers(page: Page) {
  await rowsReady(page);
  await expect(liveDot(page)).toHaveAttribute('data-state', 'live');
  await hub!.push({
    type: 'presenceChanged',
    projectId: SHELL_PROJECT.id,
    screenId: 'storiesBacklog',
    userIds: [meDto().id, PEOPLE.tom.id, PEOPLE.mei.id, PEOPLE.sam.id],
    sequence: 1,
  });
  await expect(page.getByTestId(realtimeTestIds.presenceCount)).toHaveText('+3');
}

async function openSearch(page: Page) {
  await page.getByTestId(touchTestIds.topBarSearch).tap();
  await expect(page.getByTestId(quickSearchTestIds.input)).toBeVisible();
}

phonePageShots([
  {
    name: 'top-bar',
    route: ROUTE,
    // The hub is stubbed Live, so the dot is shot as it is, not waited for Offline and masked.
    shell: false,
    arrange: async (page) => {
      await arrange(page);
      hub = await stubHub(page);
    },
    ready: liveWithThreeOthers,
  },
  {
    name: 'top-bar-reconnecting',
    route: ROUTE,
    shell: false,
    arrange: async (page) => {
      await arrange(page);
      hub = await stubHub(page);
    },
    ready: async (page) => {
      await rowsReady(page);
      await expect(liveDot(page)).toHaveAttribute('data-state', 'live');
    },
    act: async (page) => {
      await hub!.drop();
      await expect(liveDot(page)).toHaveAttribute('data-state', 'reconnecting');
      await expect(page.getByTestId(realtimeTestIds.banner)).toBeVisible();
    },
  },
  {
    name: 'search',
    route: ROUTE,
    shell: false,
    arrange,
    ready: rowsReadyOffline,
    act: async (page) => {
      // The recent list is read from storage each time the dialog opens, so seeding it here is early enough.
      await page.evaluate(
        ([key, value]) => window.localStorage.setItem(key, value),
        [`shiplean.quickSearch.recent.${meDto().id}`, JSON.stringify(recent)],
      );
      await openSearch(page);
      await expect(
        page.getByTestId(quickSearchTestIds.group).and(page.locator('[data-group="recent"]')),
      ).toBeVisible();
    },
  },
  {
    name: 'search-results',
    route: ROUTE,
    shell: false,
    arrange,
    ready: rowsReadyOffline,
    act: async (page) => {
      await openSearch(page);
      await page.getByTestId(quickSearchTestIds.input).fill('export');
      await expect(page.getByTestId(quickSearchTestIds.option).first()).toBeVisible();
      await expect(
        page.getByTestId(quickSearchTestIds.group).and(page.locator('[data-group="items"]')),
      ).toBeVisible();
    },
  },
]);
