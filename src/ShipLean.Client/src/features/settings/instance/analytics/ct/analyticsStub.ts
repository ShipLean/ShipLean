interface StubRoute {
  request(): { method(): string; postDataJSON(): unknown };
  fulfill(response: { status?: number; json?: unknown }): Promise<void>;
}

interface Routable {
  route(url: string, handler: (route: StubRoute) => Promise<void>): Promise<unknown>;
}

// The C4 shape of GET /api/instance/analytics.
export const analyticsNone = {
  provider: 'none',
  host: null,
  siteKey: null,
  sendNames: false,
};

export const analyticsUmami = {
  provider: 'umami',
  host: 'https://umami.example.com',
  siteKey: '7f3c1a52-9d4e-4b8a-a1c6-2e5f0b9d3c71',
  sendNames: false,
};

export interface AnalyticsStubOptions {
  /** What GET returns first. */
  initial?: unknown;
  /** When set, PUT answers 400 with this validation body instead of storing. */
  rejectPut?: { error: string; field: string };
  /** The POST .../test answer. */
  testResult?: { ok: boolean; message: string };
}

export interface AnalyticsStub {
  /** Every PUT body, in order. */
  puts: unknown[];
}

// Call before mounting, the way the integrations spec stubs its API. A PUT stores its body and the next GET returns it.
export const stubAnalyticsApi = async (
  page: Routable,
  options: AnalyticsStubOptions = {},
): Promise<AnalyticsStub> => {
  let current: unknown = options.initial ?? analyticsNone;
  const stub: AnalyticsStub = { puts: [] };
  await page.route('**/api/instance/analytics', (route) => {
    const request = route.request();
    if (request.method() === 'PUT') {
      stub.puts.push(request.postDataJSON());
      if (options.rejectPut) {
        return route.fulfill({
          status: 400,
          json: {
            error: options.rejectPut.error,
            code: 'validation',
            field: options.rejectPut.field,
          },
        });
      }
      current = request.postDataJSON();
    }
    return route.fulfill({ json: current });
  });
  await page.route('**/api/instance/analytics/test', (route) =>
    route.fulfill({ json: options.testResult ?? { ok: true, message: 'Test event sent.' } }),
  );
  return stub;
};
