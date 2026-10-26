import type { Ref } from 'react';
import type { QuickSearchItem, QuickSearchResults } from './types';

export interface QuickSearchDialogProps {
  query: string;
  onQueryChange(query: string): void;
  results: QuickSearchResults;
  onOpenItem(item: QuickSearchItem): void;
  onClose(): void;
  onDone(): void;
  inputRef?: Ref<HTMLInputElement>;
}

// TODO: the full dialog replaces this body.
export function QuickSearchDialog({ query }: QuickSearchDialogProps) {
  return <div>{query}</div>;
}
