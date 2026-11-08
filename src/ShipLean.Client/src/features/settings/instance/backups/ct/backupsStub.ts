import cronJson from '../../../../../../../ShipLean.TestSupport/PinnedShapes/backup-cron-preview.json' with { type: 'json' };
import runJson from '../../../../../../../ShipLean.TestSupport/PinnedShapes/backup-run.json' with { type: 'json' };
import settingsJson from '../../../../../../../ShipLean.TestSupport/PinnedShapes/backup-settings.json' with { type: 'json' };

interface StubRoute {
  request(): { method(): string; postDataJSON(): unknown };
  fulfill(response: { status?: number; json?: unknown }): Promise<void>;
}

interface Routable {
  route(url: string, handler: (route: StubRoute) => Promise<void>): Promise<unknown>;
}

// The pinned examples of GET /api/instance/backups (C-PIN).
export const backupsConfigured = settingsJson.configured;
export const backupsNotConfigured = settingsJson.notConfigured;
// The pinned valid answer of POST /api/instance/backups/cron-preview.
export const cronPreviewValid = cronJson.valid;
// The pinned runs (C-PIN): running, succeeded, failed, abandoned.
export const backupRuns = runJson;

export interface BackupsStubOptions {
  /** What GET returns. */
  initial?: unknown;
  /** When set, GET answers this error (default status 500) instead of the settings. */
  failGet?: { error: string; status?: number };
  /** When set, PUT answers this error body instead of storing. */
  rejectPut?: { error: string; field?: string; status?: number };
  /** What POST cron-preview answers for any cron (default: the pinned valid example). */
  cronPreview?: unknown;
  /** When set, POST run answers this error (default status 409) instead of starting a run. */
  rejectRun?: {
    error: string;
    code?: string;
    status?: number;
    /** Also make the next GET show a run in progress (health running, the pinned running run on top). */
    startsRunning?: boolean;
  };
}

export interface BackupsStub {
  /** Every PUT body, in order. */
  puts: unknown[];
  /** How many POST run requests came in. */
  runRequests: number;
}

// Call before mounting. A PUT stores the fields it sets over the settings, and the next GET returns them.
export const stubBackupsApi = async (
  page: Routable,
  options: BackupsStubOptions = {},
): Promise<BackupsStub> => {
  let current = (options.initial ?? backupsConfigured) as Record<string, unknown>;
  const stub: BackupsStub = { puts: [], runRequests: 0 };
  await page.route('**/api/instance/backups/cron-preview', (route) =>
    route.fulfill({ json: options.cronPreview ?? cronPreviewValid }),
  );
  // POST run starts the pinned running run: the next GET has it at the top of the history and health running.
  await page.route('**/api/instance/backups/run', (route) => {
    stub.runRequests += 1;
    if (options.rejectRun) {
      if (options.rejectRun.startsRunning) {
        const existing = (current.runs ?? []) as unknown[];
        current = { ...current, health: 'running', runs: [runJson.running, ...existing] };
      }
      return route.fulfill({
        status: options.rejectRun.status ?? 409,
        json: { error: options.rejectRun.error, code: options.rejectRun.code ?? 'conflict' },
      });
    }
    const runs = (current.runs ?? []) as unknown[];
    current = { ...current, health: 'running', runs: [runJson.running, ...runs] };
    return route.fulfill({ status: 202, json: runJson.running });
  });
  await page.route('**/api/instance/backups', (route) => {
    const request = route.request();
    if (request.method() === 'PUT') {
      const body = request.postDataJSON() as Record<string, unknown>;
      stub.puts.push(body);
      if (options.rejectPut) {
        return route.fulfill({
          status: options.rejectPut.status ?? 400,
          json: {
            error: options.rejectPut.error,
            code: 'validation',
            ...(options.rejectPut.field === undefined ? {} : { field: options.rejectPut.field }),
          },
        });
      }
      current = {
        ...current,
        enabled: body.enabled,
        cron: body.cron,
        keepCount: body.keepCount,
        includeAttachments: body.includeAttachments,
      };
      return route.fulfill({ json: current });
    }
    if (options.failGet) {
      return route.fulfill({
        status: options.failGet.status ?? 500,
        json: { error: options.failGet.error, code: 'internal_error' },
      });
    }
    return route.fulfill({ json: current });
  });
  return stub;
};
