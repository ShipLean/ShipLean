import type { ItemAsksDto } from '@shiplean/core/api/asks';
import type { ApiError } from '@shiplean/core/api/client';

// Stubs the asks routes of one work item (K4). GET asks answers the current asks; each write is recorded and answers 204,
// and the GET after it is served from whatever setAsks() set (the server's state after the write is the test's call).
// failNext(...) makes the next write answer with an ApiError instead. Call it after stubApi/stubShell: later routes win.

interface StubRoute {
  fulfill(response: { status: number; contentType?: string; body?: string }): Promise<void>;
  request(): { method(): string; url(): string; postDataJSON(): unknown };
}
interface Page {
  route(url: string, handler: (route: StubRoute) => Promise<void>): Promise<unknown>;
}

export interface AskWrite {
  method: string;
  /** The path from /api on, e.g. /api/work-items/<id>/decisions/3/change. */
  url: string;
  body: unknown;
}

export interface StubbedAsks {
  writes: AskWrite[];
  /** How many times GET asks was answered. */
  reads(): number;
  setAsks(next: ItemAsksDto): void;
  failNext(status: number, error: ApiError): void;
  /** Fails GET asks from now on (the section's error state). */
  failReads(status: number, error: ApiError): void;
}

const json = (body: unknown, status = 200) => ({
  status,
  contentType: 'application/json',
  body: JSON.stringify(body),
});

export async function stubAsks(page: Page, asks: ItemAsksDto): Promise<StubbedAsks> {
  let current = asks;
  let reads = 0;
  let failure: { status: number; error: ApiError } | null = null;
  let readFailure: { status: number; error: ApiError } | null = null;
  const writes: AskWrite[] = [];
  const base = `**/api/work-items/${asks.workItemId}`;
  await page.route(`${base}/asks`, (route) => {
    reads += 1;
    if (readFailure) return route.fulfill(json(readFailure.error, readFailure.status));
    return route.fulfill(json(current));
  });
  const write = async (route: StubRoute) => {
    const request = route.request();
    const method = request.method();
    if (method !== 'POST')
      return route.fulfill(json({ error: 'not stubbed', code: 'internal_error' }, 500));
    writes.push({ method, url: new URL(request.url()).pathname, body: request.postDataJSON() });
    if (failure) {
      const { status, error } = failure;
      failure = null;
      return route.fulfill(json(error, status));
    }
    return route.fulfill({ status: 204 });
  };
  await page.route(`${base}/decisions/**`, write);
  await page.route(`${base}/questions/**`, write);
  return {
    writes,
    reads: () => reads,
    setAsks: (next) => {
      current = next;
    },
    failNext: (status, error) => {
      failure = { status, error };
    },
    failReads: (status, error) => {
      readFailure = { status, error };
    },
  };
}
