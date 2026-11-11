import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { expect, test, vi } from 'vitest';
import { asksKey } from './asksApi';

const handlers: [string, (event: { workItemId: string }) => void][] = [];
vi.mock('../../realtime', () => ({
  registerInvalidation: (type: string, handler: (event: { workItemId: string }) => void) => {
    handlers.push([type, handler]);
    return () => {};
  },
}));

const { useAskEvents } = await import('./useAskEvents');

function Probe() {
  useAskEvents('p1', 'SHIP-141', 'item-1');
  return null;
}

test('useAskEvents_WhenAsksChangedIsForAnotherItem_ShouldNotInvalidate', () => {
  handlers.length = 0;
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  const client = new QueryClient();
  const spy = vi.spyOn(client, 'invalidateQueries');
  const root = createRoot(document.createElement('div'));
  act(() =>
    root.render(
      <QueryClientProvider client={client}>
        <Probe />
      </QueryClientProvider>,
    ),
  );
  const handler = handlers.find(([type]) => type === 'asksChanged')?.[1];
  if (!handler) throw new Error('useAskEvents did not register asksChanged');

  handler({ workItemId: 'item-2' });
  expect(spy).not.toHaveBeenCalled();

  handler({ workItemId: 'item-1' });
  expect(spy).toHaveBeenCalledWith({ queryKey: asksKey('p1', 'SHIP-141') });
  act(() => root.unmount());
});
