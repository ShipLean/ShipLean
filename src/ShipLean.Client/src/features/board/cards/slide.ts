// A card that appears in another cell slides there from where it was: its old element remembers where it stood as it
// unmounts, and the new one animates from that spot to its own. Only a card that left a moment ago slides.
const SLIDE_MS = 220;
const FRESH_MS = 1500;

const left = new Map<string, { rect: DOMRect; at: number }>();

/** Call as a card's element is removed. */
export function rememberPlace(itemId: string, element: HTMLElement) {
  left.set(itemId, { rect: element.getBoundingClientRect(), at: performance.now() });
}

/** Call as a card's element is added. */
export function slideIn(itemId: string, element: HTMLElement) {
  const before = left.get(itemId);
  left.delete(itemId);
  if (!before || performance.now() - before.at > FRESH_MS) return;
  if (typeof element.animate !== 'function') return;
  if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;
  const now = element.getBoundingClientRect();
  const dx = before.rect.left - now.left;
  const dy = before.rect.top - now.top;
  if (dx === 0 && dy === 0) return;
  element.animate(
    [{ transform: `translate(${dx}px, ${dy}px)` }, { transform: 'translate(0, 0)' }],
    { duration: SLIDE_MS, easing: 'ease-out' },
  );
}
