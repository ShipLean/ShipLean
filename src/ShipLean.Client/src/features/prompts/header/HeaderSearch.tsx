import { useState } from 'react';
import type { PromptKey, PromptsPageDto } from '@shiplean/core/api/prompts';
import { promptsTestIds as ids } from '../testIds';
import { filterPrompts } from './filterPrompts';

// Search in the page header (D5): the prompts whose name, file, text in use or placeholders match; a click or Enter
// opens one. TODO: a minimal list of matching names; the results menu (prompt, stage, file, status chip, first matching
// line) replaces it.
export function HeaderSearch({
  page,
  onPick,
}: {
  page: PromptsPageDto;
  onPick: (key: PromptKey) => void;
}) {
  const [query, setQuery] = useState('');
  const hits =
    query.trim() === '' ? [] : filterPrompts(page.stages, query).flatMap((s) => s.prompts);
  const pick = (key: PromptKey) => {
    setQuery('');
    onPick(key);
  };
  return (
    <div className="relative min-w-0">
      <input
        type="search"
        data-testid={ids.searchInput}
        className="field"
        placeholder="Find a prompt or placeholder"
        aria-label="Find a prompt"
        autoComplete="off"
        value={query}
        onChange={(e) => setQuery(e.target.value)}
        onKeyDown={(e) => {
          if (e.key === 'Enter' && hits.length > 0) pick(hits[0].key);
        }}
      />
      {query.trim() === '' ? null : (
        <ul
          data-testid={ids.searchResults}
          aria-label="Matching prompts"
          className="absolute right-0 z-10 m-0 mt-1 grid min-w-full list-none gap-0.5 rounded-control border border-edge bg-panel p-1"
        >
          {hits.length === 0 ? (
            <li className="p-1.5 text-ink-muted">No prompt matches</li>
          ) : (
            hits.map((prompt) => (
              <li key={prompt.key}>
                <button
                  type="button"
                  data-testid={ids.searchResult}
                  data-prompt={prompt.key}
                  className="w-full p-1.5 text-left hover:bg-sunken touch:min-h-[var(--touch-target)]"
                  onClick={() => pick(prompt.key)}
                >
                  {prompt.name}
                </button>
              </li>
            ))
          )}
        </ul>
      )}
    </div>
  );
}
