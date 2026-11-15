import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { MemoryRouter, useSearchParams } from 'react-router-dom';
import { expect, test } from 'vitest';
import { useOpenCardSettings } from './useOpenCardSettings';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

function Probe() {
  const open = useOpenCardSettings();
  const [params] = useSearchParams();
  return (
    <button type="button" data-query={params.toString()} onClick={open}>
      open
    </button>
  );
}

test('UseOpenCardSettings_WhenCalled_ShouldSetSettingsAndKeepTheRestOfTheQuery', async () => {
  const host = document.createElement('div');
  const root = createRoot(host);
  await act(async () => {
    root.render(
      <MemoryRouter initialEntries={['/board?group=people']}>
        <Probe />
      </MemoryRouter>,
    );
  });

  await act(async () => host.querySelector('button')?.click());

  const query = new URLSearchParams(host.querySelector('button')?.getAttribute('data-query') ?? '');
  expect(query.get('settings')).toBe('fields');
  expect(query.get('group')).toBe('people');
  await act(async () => root.unmount());
});
