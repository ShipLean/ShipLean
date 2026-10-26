import { useLayoutEffect, useRef, type RefObject } from 'react';

const SLIDE_MS = 300;

/**
 * Slides the rows a teammate just changed (`data-flash="true"`) from where they were to where the new read puts them,
 * across sections too. Other rows jump as before, so a drop of your own is not animated twice.
 */
export function useRowFlip(table: RefObject<HTMLElement | null>) {
  const last = useRef<Map<string, number> | null>(null);
  useLayoutEffect(() => {
    const element = table.current;
    if (element === null) return;
    const rows = Array.from(element.querySelectorAll<HTMLElement>('[data-row-id]'));
    const tops = new Map(
      rows.map((row) => [row.dataset.rowId ?? '', row.getBoundingClientRect().top]),
    );
    const previous = last.current;
    last.current = tops;
    if (previous === null || window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;
    for (const row of rows) {
      const id = row.dataset.rowId ?? '';
      const was = previous.get(id);
      const now = tops.get(id);
      if (row.dataset.flash !== 'true' || was === undefined || now === undefined || was === now)
        continue;
      row.animate([{ transform: `translateY(${was - now}px)` }, { transform: 'translateY(0)' }], {
        duration: SLIDE_MS,
        easing: 'ease-out',
      });
    }
  });
}
