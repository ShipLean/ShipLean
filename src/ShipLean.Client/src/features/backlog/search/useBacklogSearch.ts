import { useMemo, useState } from 'react';
import type { BacklogRowDto } from '@shiplean/core/api/views';
import { useListCommand } from '../commands/listCommands';

export interface BacklogSearch {
  /** The rows that match, in their order. */
  apply: (rows: BacklogRowDto[]) => BacklogRowDto[];
  /** A search is narrowing the rows. */
  active: boolean;
  /** What is typed in the box. */
  query: string;
  setQuery: (query: string) => void;
  /** The box, so the `search` command can focus it. */
  attachBox: (box: HTMLInputElement | null) => void;
}

/** Whether a row's key or title holds the text, ignoring case. */
export function matchesSearch(row: BacklogRowDto, text: string): boolean {
  const needle = text.trim().toLowerCase();
  return row.key.toLowerCase().includes(needle) || row.title.toLowerCase().includes(needle);
}

/** The screen's search over the rows it has loaded: key and title, case-insensitive, no request. Registers the `search` command. */
export function useBacklogSearch(): BacklogSearch {
  const [query, setQuery] = useState('');
  const [box, attachBox] = useState<HTMLInputElement | null>(null);
  useListCommand('search', () => {
    box?.focus();
    box?.select();
  });
  const active = query.trim() !== '';
  return useMemo(
    () => ({
      apply: (rows) => (active ? rows.filter((row) => matchesSearch(row, query)) : rows),
      active,
      query,
      setQuery,
      attachBox,
    }),
    [active, query, attachBox],
  );
}
