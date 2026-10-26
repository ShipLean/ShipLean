import { createContext, useContext } from 'react';

export interface QuickSearchControl {
  open(): void;
  close(): void;
  isOpen: boolean;
}

export const QuickSearchContext = createContext<QuickSearchControl | undefined>(undefined);

export function useQuickSearch(): QuickSearchControl {
  const control = useContext(QuickSearchContext);
  if (control === undefined) throw new Error('useQuickSearch needs a QuickSearchProvider');
  return control;
}
