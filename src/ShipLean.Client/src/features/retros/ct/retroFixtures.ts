import type { test } from '@playwright/experimental-ct-react';
import type {
  RetroActionDetailDto,
  RetroActionDialogDto,
  RetroActionRefDto,
  RetroFeaturesDto,
  RetroInsightsDto,
  RetroItemDto,
  RetroPointDto,
  RetroSearchDto,
  RetroThemeListDto,
  RetroThemeRefDto,
  SprintRetroDto,
  WorkItemRetrosDto,
} from '@shiplean/core/api/retros';
import actionDetailFixture from '../../../../../ShipLean.TestSupport/DtoFixtures/retros/retro-action-detail.json' with { type: 'json' };
import actionDialogFixture from '../../../../../ShipLean.TestSupport/DtoFixtures/retros/retro-action-dialog.json' with { type: 'json' };
import featuresFixture from '../../../../../ShipLean.TestSupport/DtoFixtures/retros/retro-features.json' with { type: 'json' };
import itemFixture from '../../../../../ShipLean.TestSupport/DtoFixtures/retros/retro-item.json' with { type: 'json' };
import insightsFixture from '../../../../../ShipLean.TestSupport/DtoFixtures/retros/retro-insights.json' with { type: 'json' };
import pointFixture from '../../../../../ShipLean.TestSupport/DtoFixtures/retros/retro-point.json' with { type: 'json' };
import searchFixture from '../../../../../ShipLean.TestSupport/DtoFixtures/retros/retro-search.json' with { type: 'json' };
import themesFixture from '../../../../../ShipLean.TestSupport/DtoFixtures/retros/retro-themes.json' with { type: 'json' };
import sprintFixture from '../../../../../ShipLean.TestSupport/DtoFixtures/retros/sprint-retro.json' with { type: 'json' };
import workItemFixture from '../../../../../ShipLean.TestSupport/DtoFixtures/retros/work-item-retros.json' with { type: 'json' };

type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];

// The pinned fixtures, typed. Every stubbed answer below is one of them, as the server's contract says.
export const sprintRetro = sprintFixture as unknown as SprintRetroDto;
export const retroSearch = searchFixture as unknown as RetroSearchDto;
export const retroInsights = insightsFixture as unknown as RetroInsightsDto;
export const retroThemes = themesFixture as unknown as RetroThemeListDto;
export const workItemRetros = workItemFixture as unknown as WorkItemRetrosDto;
export const retroPoint = pointFixture as unknown as RetroPointDto;

export const retroFeatures = featuresFixture as unknown as RetroFeaturesDto;
export const retroItem = itemFixture as unknown as RetroItemDto;
export const actionDialog = actionDialogFixture as unknown as RetroActionDialogDto;
export const retroActionDetail = actionDetailFixture as unknown as RetroActionDetailDto;

const itemPoints = retroItem.categories.flatMap((column) => column.groups.flatMap((g) => g.points));
const pointWhere = (test: (point: RetroPointDto) => boolean, what: string) => {
  const found = itemPoints.find(test);
  if (found === undefined) throw new Error(`The retro-item fixture has no ${what} point.`);
  return found;
};

/** One point per state a note can be in, from the pinned retro-item fixture (a Went well point has no decision). */
export const noteStates = {
  wentWell: pointWhere((p) => p.category === 'wentWell', 'Went well'),
  undecided: pointWhere((p) => p.decision === 'undecided', 'undecided'),
  notNeeded: pointWhere((p) => p.decision === 'notNeeded', 'not needed'),
  actioned: retroPoint,
} satisfies Record<string, RetroPointDto>;

/** The Insights answer in week mode: six weeks as columns, no sprints, every count parallel to the columns. */
export const insightsWeeks: RetroInsightsDto = {
  ...retroInsights,
  range: 'w6',
  mode: 'week',
  sprints: [],
  columns: ['2026-09-21', '2026-09-28', '2026-10-05', '2026-10-12', '2026-10-19', '2026-10-26'].map(
    (key, index, all) => ({
      key,
      label: ['21 Sep', '28 Sep', '5 Oct', '12 Oct', '19 Oct', '26 Oct'][index],
      kind: 'week' as const,
      isCurrent: index === all.length - 1,
    }),
  ),
};

/** The action item the first Can be improved point already has, used as the answer to creating one. */
export const retroActionRef: RetroActionRefDto = sprintRetro.actionItems[0].action;

/** A read or a write the screen made. `query` is the raw query string without the "?". */
export interface RetroCall {
  method: string;
  path: string;
  query: string;
  body: unknown;
}

export interface RetroStub {
  /** Every retro call so far, in order. */
  calls: RetroCall[];
  /** Only the writes (POST and PUT). */
  writes: () => RetroCall[];
  /** The reads of one route (a path ending), e.g. reads('/retros/insights'). */
  reads: (pathEnd: string) => RetroCall[];
}

/**
 * Stubs every retrospective route (SHIP-82 contract C6) with the pinned fixtures and records the calls. The insights
 * answer echoes the `range` asked for. Register it after the shell stub: the last route registered answers first.
 */
export async function stubRetros(page: Page): Promise<RetroStub> {
  const calls: RetroCall[] = [];
  const json = (body: unknown, status = 200) => ({
    status,
    contentType: 'application/json',
    body: JSON.stringify(body),
  });
  const handle =
    (answer: (call: RetroCall) => ReturnType<typeof json> | { status: number }) =>
    async (route: {
      request(): { method(): string; url(): string; postDataJSON(): unknown };
      fulfill(response: object): Promise<void>;
    }) => {
      const request = route.request();
      const url = new URL(request.url());
      const method = request.method();
      const body = method === 'GET' ? null : (request.postDataJSON() as unknown);
      const call = { method, path: url.pathname, query: url.search.slice(1), body };
      calls.push(call);
      await route.fulfill(answer(call));
    };
  const at = (pattern: RegExp) => (url: URL) => pattern.test(url.pathname);

  await page.route(
    at(/^\/api\/sprints\/[^/]+\/retro$/),
    handle(() => json(sprintRetro)),
  );
  await page.route(
    at(/^\/api\/projects\/[^/]+\/retros$/),
    handle(() => json(retroSearch)),
  );
  await page.route(
    at(/^\/api\/projects\/[^/]+\/retros\/insights$/),
    handle((call) => {
      const range = new URLSearchParams(call.query).get('range') ?? '6';
      if (range === 'w6' || range === 'w12') return json({ ...insightsWeeks, range });
      return json({ ...retroInsights, range });
    }),
  );
  await page.route(
    at(/^\/api\/projects\/[^/]+\/retros\/themes$/),
    handle(() => json(retroThemes)),
  );
  await page.route(
    at(/^\/api\/work-items\/[^/]+\/retros$/),
    handle(() => json(workItemRetros)),
  );
  await page.route(
    at(/^\/api\/projects\/[^/]+\/retros\/points$/),
    handle(() => json(retroPoint, 201)),
  );
  await page.route(
    at(/^\/api\/retros\/points\/[^/]+\/theme$/),
    handle(() => json(retroPoint)),
  );
  await page.route(
    at(/^\/api\/retros\/points\/[^/]+\/archive$/),
    handle(() => ({ status: 204 })),
  );
  await page.route(
    at(/^\/api\/retros\/points\/[^/]+\/action$/),
    handle(() => json(retroActionRef, 201)),
  );
  await page.route(
    at(/^\/api\/retros\/themes\/[^/]+$/),
    handle((call) => {
      const name = (call.body as { name: string }).name;
      const theme: RetroThemeRefDto = { ...retroThemes.themes[0].theme, name };
      return json(theme);
    }),
  );
  await page.route(
    at(/^\/api\/projects\/[^/]+\/retros\/themes\/merge$/),
    handle(() => ({ status: 204 })),
  );
  await page.route(
    at(/^\/api\/projects\/[^/]+\/retros\/features$/),
    handle(() => json(retroFeatures)),
  );
  await page.route(
    at(/^\/api\/projects\/[^/]+\/retros\/items\/[^/]+$/),
    handle(() => json(retroItem)),
  );
  await page.route(
    at(/^\/api\/projects\/[^/]+\/retros\/action-dialog$/),
    handle(() => json(actionDialog)),
  );
  await page.route(
    at(/^\/api\/projects\/[^/]+\/retros\/actions$/),
    handle(() => json(retroActionDetail, 201)),
  );
  await page.route(
    at(/^\/api\/retros\/actions\/[^/]+$/),
    handle(() => json(retroActionDetail)),
  );
  await page.route(
    at(/^\/api\/retros\/actions\/[^/]+\/points$/),
    handle(() => json(retroActionDetail)),
  );
  await page.route(
    at(/^\/api\/retros\/points\/[^/]+\/not-needed$/),
    handle(() => json(noteStates.notNeeded)),
  );
  await page.route(
    at(/^\/api\/retros\/points\/[^/]+\/undo-not-needed$/),
    handle(() => json(noteStates.undecided)),
  );
  await page.route(
    at(/^\/api\/retros\/theme-merge-proposals\/[^/]+\/(merge|keep-apart)$/),
    handle(() => ({ status: 204 })),
  );
  return {
    calls,
    writes: () => calls.filter((c) => c.method !== 'GET'),
    reads: (pathEnd) => calls.filter((c) => c.method === 'GET' && c.path.endsWith(pathEnd)),
  };
}

// SignalR frames end with the record separator.
const RS = '\u001e';

/**
 * Stands in for the project hub (docs/events.md): answers the negotiate, the handshake and every hub call, and lets the
 * test push a project event to the page. Needed to see a screen react to a `retroChanged` another tab caused.
 * It reloads the page (socket routing reaches only documents loaded after it), so call it before `mount`.
 */
export async function stubHub(page: Page): Promise<{ push: (event: object) => Promise<void> }> {
  let open: () => void = () => undefined;
  const opened = new Promise<void>((resolve) => {
    open = resolve;
  });
  let socket: { send(message: string): void } | undefined;
  await page.route(
    (url) => url.pathname === '/hubs/project/negotiate',
    (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          negotiateVersion: 1,
          connectionId: 'hub-1',
          connectionToken: 'hub-token-1',
          availableTransports: [{ transport: 'WebSockets', transferFormats: ['Text', 'Binary'] }],
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
  // Socket routing is an init script, so it reaches only documents loaded after it: load the component page again.
  await page.reload();
  return {
    // Waits for the page to open the hub, then sends the event.
    push: async (event) => {
      await opened;
      socket?.send(
        `${JSON.stringify({ type: 1, target: 'projectEvent', arguments: [event] })}${RS}`,
      );
    },
  };
}
