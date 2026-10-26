import type { Ref } from 'react';
import { QuickSearchDialog } from '../QuickSearchDialog';
import { useOpenItem } from './useOpenItem';
import { useQuickSearchResults } from './useQuickSearchResults';

// Mounted only while quick search is open, so the sources run only then and read again on every open.
export function OpenQuickSearch({
  query,
  onQueryChange,
  inputRef,
  onClose,
}: {
  query: string;
  onQueryChange: (query: string) => void;
  inputRef: Ref<HTMLInputElement>;
  onClose: () => void;
}) {
  const results = useQuickSearchResults(query);
  const openItem = useOpenItem();
  return (
    <QuickSearchDialog
      query={query}
      onQueryChange={onQueryChange}
      results={results}
      onOpenItem={openItem}
      onClose={onClose}
      onDone={onClose}
      inputRef={inputRef}
    />
  );
}
