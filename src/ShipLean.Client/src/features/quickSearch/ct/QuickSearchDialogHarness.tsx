import { useState } from 'react';
import { QuickSearchDialog } from '../QuickSearchDialog';
import type { QuickSearchResults, QuickSearchRow } from '../types';
import { quickSearchDialogHarnessIds as ids } from './harnessIds';

// The dialog with results passed in. It owns the query and writes what the dialog called into text a test can read:
// the keys of the items opened, the ids of the sprint/project/action handlers run, and how often it closed or finished.
export function QuickSearchDialogHarness({
  results,
  initialQuery = '',
}: {
  results: QuickSearchResults;
  initialQuery?: string;
}) {
  const [query, setQuery] = useState(initialQuery);
  const [opened, setOpened] = useState<string[]>([]);
  const [closed, setClosed] = useState(0);
  const [done, setDone] = useState(0);
  const [ran, setRan] = useState<string[]>([]);
  // Spec data cannot carry handlers across the mount boundary: every sprint, project and action records its id.
  const record = (row: QuickSearchRow): QuickSearchRow => {
    const note = () => setRan((all) => [...all, row.id]);
    if (row.kind === 'sprint' || row.kind === 'project') return { ...row, open: note };
    if (row.kind === 'action') return { ...row, run: note };
    return row;
  };
  const shown = results;
  const recorded: QuickSearchResults = {
    ...shown,
    groups: shown.groups.map((group) => ({ ...group, rows: group.rows.map(record) })),
  };
  return (
    <div>
      <span data-testid={ids.opened}>{opened.join(',')}</span>
      <span data-testid={ids.closed}>{closed}</span>
      <span data-testid={ids.done}>{done}</span>
      <span data-testid={ids.query}>{query}</span>
      <span data-testid={ids.ran}>{ran.join(',')}</span>
      <QuickSearchDialog
        query={query}
        onQueryChange={setQuery}
        results={recorded}
        onOpenItem={(item) => setOpened((all) => [...all, item.key])}
        onClose={() => setClosed((count) => count + 1)}
        onDone={() => setDone((count) => count + 1)}
      />
    </div>
  );
}
