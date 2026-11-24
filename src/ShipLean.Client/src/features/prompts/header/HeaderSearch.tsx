import { useId, useState } from 'react';
import type { PromptKey, PromptsPageDto } from '@shiplean/core/api/prompts';
import { promptsSearchPicked } from '@shiplean/core/api/usageApi';
import { promptsTestIds as ids } from '../testIds';
import { filterPrompts } from './filterPrompts';
import { SearchHit } from './search/SearchHit';

// Search in the page header with a results menu (D5): the prompts whose name, file, text in use or placeholders match.
// Each hit shows its stage and file, its status chip and the first matching line. Enter opens the highlighted hit (the
// first until the arrow keys move it), Escape or a click elsewhere closes the menu. A pick is reported as
// promptsSearchPicked (D17) and goes through onPick, where the page keeps its leave guard.
export function HeaderSearch({
  projectId,
  page,
  onPick,
}: {
  projectId: string;
  page: PromptsPageDto;
  onPick: (key: PromptKey) => void;
}) {
  const [query, setQuery] = useState('');
  const [menuOpen, setMenuOpen] = useState(false);
  const [active, setActive] = useState(0);
  const listId = useId();

  const searching = query.trim() !== '';
  const total = page.stages.reduce((n, stage) => n + stage.prompts.length, 0);
  const hits = searching
    ? filterPrompts(page.stages, query).flatMap((stage) => {
        const number = page.stages.findIndex((s) => s.id === stage.id) + 1;
        return stage.prompts.map((prompt) => ({ prompt, stageLabel: `${number} · ${stage.name}` }));
      })
    : [];
  const showMenu = searching && menuOpen;
  const hitId = (index: number) => `${listId}-${index}`;

  const pick = (key: PromptKey) => {
    setQuery('');
    setMenuOpen(false);
    void promptsSearchPicked(projectId, key).catch(() => undefined);
    onPick(key);
  };

  return (
    <div
      className="relative w-[300px] min-w-0 touch:w-full"
      onBlur={(e) => {
        if (!e.currentTarget.contains(e.relatedTarget)) setMenuOpen(false);
      }}
    >
      <input
        type="search"
        role="combobox"
        data-testid={ids.searchInput}
        className="field w-full"
        placeholder="Find a prompt or placeholder"
        aria-label="Find a prompt"
        aria-expanded={showMenu}
        aria-controls={listId}
        aria-activedescendant={showMenu && hits.length > 0 ? hitId(active) : undefined}
        autoComplete="off"
        value={query}
        onChange={(e) => {
          setQuery(e.target.value);
          setActive(0);
          setMenuOpen(true);
        }}
        onFocus={() => setMenuOpen(true)}
        onKeyDown={(e) => {
          if (e.key === 'Escape') {
            if (showMenu) e.preventDefault();
            setMenuOpen(false);
          } else if (e.key === 'ArrowDown' && hits.length > 0) {
            e.preventDefault();
            setMenuOpen(true);
            setActive((i) => Math.min(i + 1, hits.length - 1));
          } else if (e.key === 'ArrowUp' && hits.length > 0) {
            e.preventDefault();
            setActive((i) => Math.max(i - 1, 0));
          } else if (e.key === 'Enter' && showMenu && hits.length > 0) {
            e.preventDefault();
            pick(hits[Math.min(active, hits.length - 1)].prompt.key);
          }
        }}
      />
      {showMenu ? (
        <div
          id={listId}
          role="listbox"
          aria-label="Matching prompts"
          data-testid={ids.searchResults}
          className="absolute right-0 top-full z-20 mt-1 max-h-[420px] w-[440px] overflow-auto rounded-panel border border-edge bg-panel p-1 shadow-[var(--shadow-dialog)] touch:w-full"
        >
          {hits.length === 0 ? (
            <p
              data-testid={ids.searchEmpty}
              className="m-0 px-[9px] py-2 text-[length:var(--text-sm)] text-ink-muted"
            >
              No prompt matches. Placeholders are searchable too, for example{' '}
              <code className="font-mono">task_key</code>.
            </p>
          ) : (
            <>
              {hits.map(({ prompt, stageLabel }, index) => (
                <SearchHit
                  key={prompt.key}
                  id={hitId(index)}
                  prompt={prompt}
                  stageLabel={stageLabel}
                  query={query}
                  active={index === active}
                  onPick={() => pick(prompt.key)}
                />
              ))}
              <p
                data-testid={ids.searchFoot}
                className="m-0 mt-1 border-t border-edge px-[9px] pt-1.5 pb-1 text-[length:var(--text-2xs)] text-ink-muted"
              >
                {hits.length} of {total} prompts · Enter opens the first
              </p>
            </>
          )}
        </div>
      ) : null}
    </div>
  );
}
