import { vi } from 'vitest';
import { EVENT_METHOD, type RealtimeConnection } from '../connection';

// A hub connection that does what the test tells it to (not production code: only tests import it).
export function fakeConnection(startResults: ('ok' | 'fail')[] = []) {
  const handlers: Record<string, (event: unknown) => void> = {};
  let closed: () => void = () => undefined;
  const calls: string[] = [];
  const connection = {
    start: vi.fn(async () => {
      if (startResults.shift() === 'fail') throw new Error('offline');
    }),
    stop: vi.fn(async () => undefined),
    invoke: vi.fn(async (method: string, ...args: unknown[]) => {
      calls.push([method, ...args].join(' '));
    }),
    on: vi.fn((method: string, handler: (event: unknown) => void) => {
      handlers[method] = handler;
    }),
    onclose: vi.fn((handler: () => void) => {
      closed = handler;
    }),
  };
  return {
    connection: connection as unknown as RealtimeConnection,
    spies: connection,
    /** Every hub call so far, as "Method arg arg". */
    calls,
    deliver: (event: unknown) => handlers[EVENT_METHOD]?.(event),
    drop: () => closed(),
  };
}
