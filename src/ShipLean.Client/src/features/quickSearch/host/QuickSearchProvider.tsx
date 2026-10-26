import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { quickSearchTestIds } from '../testIds';
import { OpenQuickSearch } from './OpenQuickSearch';
import { QuickSearchContext } from './useQuickSearch';

// Holds whether quick search is open and what was last typed (kept while closed, so reopening shows the last search),
// listens for Ctrl/⌘+P, and mounts the dialog while it is open.
export function QuickSearchProvider({ children }: { children: ReactNode }) {
  const [isOpen, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const input = useRef<HTMLInputElement>(null);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (!(event.ctrlKey || event.metaKey) || event.shiftKey || event.key.toLowerCase() !== 'p') {
        return;
      }
      // Another modal dialog being open wins; quick search's own dialog is modal too.
      const modal = document.querySelector(
        `[aria-modal="true"]:not([data-testid="${quickSearchTestIds.dialog}"])`,
      );
      if (modal) return;
      event.preventDefault();
      if (isOpen) {
        input.current?.focus();
        input.current?.select();
      } else setOpen(true);
    };
    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, [isOpen]);

  const control = useMemo(
    () => ({ open: () => setOpen(true), close: () => setOpen(false), isOpen }),
    [isOpen],
  );

  return (
    <QuickSearchContext value={control}>
      {children}
      {isOpen ? (
        <OpenQuickSearch
          query={query}
          onQueryChange={setQuery}
          inputRef={input}
          onClose={control.close}
        />
      ) : null}
    </QuickSearchContext>
  );
}
