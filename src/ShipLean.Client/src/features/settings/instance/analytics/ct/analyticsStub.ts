interface Routable {
  route(
    url: string,
    handler: (route: { fulfill(response: { json?: unknown }): Promise<void> }) => Promise<void>,
  ): Promise<unknown>;
}

// The C4 shape of GET /api/instance/analytics. B2 adds the PUT and test stubs next to this one.
export const analyticsNone = {
  provider: 'none',
  host: null,
  siteKey: null,
  sendNames: false,
};

// Call before mounting, the way the integrations spec stubs its API.
export const stubAnalyticsApi = async (page: Routable) => {
  await page.route('**/api/instance/analytics', (route) => route.fulfill({ json: analyticsNone }));
};
