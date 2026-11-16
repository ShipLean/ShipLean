import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test } from 'vitest';
import { useOfferDismissal } from './useOfferDismissal';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

// One copy of a story's header, as a phone page draws it: a button that shows whether the offer is dismissed.
function Copy({ id, offered, shared }: { id: string; offered: boolean; shared: boolean }) {
  const [dismissed, dismiss] = useOfferDismissal(id, offered, shared);
  return (
    <button type="button" data-id={id} onClick={dismiss}>
      {dismissed ? 'dismissed' : 'offered'}
    </button>
  );
}

let host: HTMLElement;
let root: Root;
beforeEach(() => {
  host = document.createElement('div');
  document.body.append(host);
  root = createRoot(host);
});
afterEach(() => {
  act(() => root.unmount());
  host.remove();
});

const render = (copies: { offered?: boolean; shared?: boolean }[]) =>
  act(() =>
    root.render(
      <>
        {copies.map((c, i) => (
          <Copy key={i} id="s1" offered={c.offered ?? true} shared={c.shared ?? true} />
        ))}
      </>,
    ),
  );
const texts = () => [...host.querySelectorAll('button')].map((b) => b.textContent);
const clickFirst = () => act(() => host.querySelector('button')!.click());

test('useOfferDismissal_WhenDismissedOnOnePage_ShouldHideTheOfferOnEveryPage', () => {
  render([{}, {}, {}]);
  clickFirst();
  expect(texts()).toEqual(['dismissed', 'dismissed', 'dismissed']);
});

test('useOfferDismissal_WhenBoardRemounted_ShouldStartUndismissed', () => {
  render([{}, {}]);
  clickFirst();
  act(() => root.unmount());
  root = createRoot(host);
  render([{}, {}]);
  expect(texts()).toEqual(['offered', 'offered']);
});

test('useOfferDismissal_WhenOfferGoesAwayAndComesBack_ShouldShowAgain', () => {
  render([{}, {}]);
  clickFirst();
  render([{ offered: false }, { offered: false }]);
  render([{}, {}]);
  expect(texts()).toEqual(['offered', 'offered']);
});

test('useOfferDismissal_WhenNotShared_ShouldKeepEachCopyOwn', () => {
  render([{ shared: false }, { shared: false }]);
  clickFirst();
  expect(texts()).toEqual(['dismissed', 'offered']);
});
