import { useState } from 'react';
import { Route, Routes, useLocation } from 'react-router-dom';
import { QuickSearchDialog } from '../../QuickSearchDialog';
import { useSearchSource } from '../useSearchSource';
import { searchSourceHarnessIds as ids } from './harnessIds';

function Dialog() {
  const [query, setQuery] = useState('');
  const [opened, setOpened] = useState<string[]>([]);
  const location = useLocation();
  const results = useSearchSource(query);
  return (
    <div>
      <span data-testid={ids.location}>{location.pathname}</span>
      <span data-testid={ids.opened}>{opened.join(',')}</span>
      <span data-testid={ids.query}>{query}</span>
      <QuickSearchDialog
        query={query}
        onQueryChange={setQuery}
        results={results}
        onOpenItem={(item) => setOpened((all) => [...all, item.key])}
        onClose={() => undefined}
        onDone={() => undefined}
      />
    </div>
  );
}

// The dialog fed by the real search source, under a project route so the shell's key resolves.
export function SearchSourceHarness() {
  return (
    <Routes>
      <Route path="/p/:key/*" element={<Dialog />} />
    </Routes>
  );
}
