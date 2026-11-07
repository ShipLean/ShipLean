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

export interface BackupsStubOptions {
  /** What GET returns. */
  initial?: unknown;
  /** When set, GET answers this error (default status 500) instead of the settings. */
  failGet?: { error: string; status?: number };
  /** When set, PUT answers this error body instead of storing. */
  rejectPut?: { error: string; field?: string; status?: number };
}

export interface BackupsStub {
  /** Every PUT body, in order. */
  puts: unknown[];
}

// Call before mounting. A PUT stores the fields it sets over the settings, and the next GET returns them.
export const stubBackupsApi = async (
  page: Routable,
  options: BackupsStubOptions = {},
): Promise<BackupsStub> => {
  let current = (options.initial ?? backupsConfigured) as Record<string, unknown>;
  const stub: BackupsStub = { puts: [] };
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
