import { useEffect } from 'react';

/**
 * Calls `onClose` when something the opener sits in scrolls sideways: a popover placed once, in fixed coordinates, would
 * stay behind. Vertical scrolling and scrolling that is not an ancestor's (a focus that scrolls a card into view) leave it.
 */
export function useCloseOnHorizontalScroll(opener: () => Element | null, onClose: () => void) {
  useEffect(() => {
    const started = new Map<Element, number>();
    for (let node = opener(); node; node = node.parentElement) started.set(node, node.scrollLeft);
    const onScroll = (event: Event) => {
      const target =
        event.target instanceof Document ? event.target.scrollingElement : event.target;
      if (!(target instanceof Element)) return;
      const was = started.get(target);
      if (was !== undefined && target.scrollLeft !== was) onClose();
    };
    document.addEventListener('scroll', onScroll, true);
    return () => document.removeEventListener('scroll', onScroll, true);
  }, [opener, onClose]);
}
