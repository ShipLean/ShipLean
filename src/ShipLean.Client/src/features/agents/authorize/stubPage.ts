// The part of a Playwright page and route the agents' component tests use (Playwright's own types are not importable
// here). Nothing in this file is production code.
export interface StubRoute {
  request(): { method(): string; url(): string; postDataJSON(): unknown };
  fulfill(options: { status: number; contentType: string; body: string }): Promise<void>;
  fallback(): Promise<void>;
}

export interface StubbablePage {
  route(url: string, handler: (route: StubRoute) => Promise<void>): Promise<unknown>;
}
