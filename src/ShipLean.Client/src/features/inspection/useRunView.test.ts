import { act, createElement, useEffect } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { MemoryRouter, useLocation } from 'react-router-dom';
import { afterEach, beforeAll, expect, test } from 'vitest';
import { useRunView, type RunView } from './useRunView';

let root: Root | undefined;
const seen: { view: RunView; search: string } = { view: undefined as never, search: '' };

beforeAll(() => {
  // React's act needs to know this is a test environment.
  (globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
});

afterEach(() => {
  act(() => root?.unmount());
  root = undefined;
});

function Probe() {
  const view = useRunView();
  const { search } = useLocation();
  useEffect(() => {
    seen.view = view;
    seen.search = search;
  });
  return null;
}

function mount(initial: string) {
  const host = document.createElement('div');
  root = createRoot(host);
  act(() =>
    root?.render(createElement(MemoryRouter, { initialEntries: [initial] }, createElement(Probe))),
  );
}

test('useRunView_WhenQueryHasEveryPart_ShouldReadThem', () => {
  mount('/runs/r1?turn=2&errorsOnly=true&row=abc&q=test%20run');
  expect(seen.view).toMatchObject({
    zoomTurn: 2,
    errorsOnly: true,
    selectedRowId: 'abc',
    query: 'test run',
  });
});

test('useRunView_WhenQueryEmpty_ShouldBeWholeRunNoSelection', () => {
  mount('/runs/r1');
  expect(seen.view).toMatchObject({
    zoomTurn: null,
    errorsOnly: false,
    selectedRowId: null,
    query: '',
  });
});

test('useRunView_WhenSetters_ShouldRoundTripThroughTheUrlKeepingTheRest', () => {
  mount('/runs/r1?other=1');
  act(() => seen.view.setZoomTurn(3));
  act(() => seen.view.setErrorsOnly(true));
  act(() => seen.view.selectRow('r9'));
  act(() => seen.view.setQuery('fail'));
  expect(seen.search).toBe('?other=1&turn=3&errorsOnly=true&row=r9&q=fail');
  expect(seen.view).toMatchObject({
    zoomTurn: 3,
    errorsOnly: true,
    selectedRowId: 'r9',
    query: 'fail',
  });

  act(() => seen.view.setZoomTurn(null));
  act(() => seen.view.setErrorsOnly(false));
  act(() => seen.view.selectRow(null));
  act(() => seen.view.setQuery(''));
  expect(seen.search).toBe('?other=1');
});
