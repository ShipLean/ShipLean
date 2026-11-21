import { useMemo, useState } from 'react';
import type { PromptDto, PromptKey, PromptsPageDto } from '@shiplean/core/api/prompts';
import { Chip, type ChipTone } from '../../../components/Chip';
import { promptsTestIds as ids } from '../testIds';
import { filterPrompts, type PromptFilter } from './filterPrompts';
import { stageAnchor } from './StageStrip';

const TONES: Record<PromptDto['status'], ChipTone> = {
  builtIn: 'muted',
  custom: 'purple',
  builtInUpdated: 'warn',
  usingBuiltIn: 'muted',
};

function chipLabel(prompt: PromptDto): string {
  switch (prompt.status) {
    case 'builtIn':
      return 'Built-in';
    case 'custom':
      return `Custom v${prompt.custom?.number}`;
    case 'builtInUpdated':
      return 'Built-in updated';
    case 'usingBuiltIn':
      return 'Using built-in';
  }
}

const FILTERS: { id: PromptFilter; label: string; testId: string }[] = [
  { id: 'all', label: 'All', testId: ids.filterAll },
  { id: 'custom', label: 'Custom', testId: ids.filterCustom },
];

// The prompts grouped by stage on a numbered rail, with search and the All / Custom filter; selecting one calls onSelect.
export function PromptList({
  page,
  selectedKey,
  onSelect,
}: {
  page: PromptsPageDto;
  selectedKey: PromptKey | null;
  onSelect: (key: PromptKey) => void;
}) {
  const [query, setQuery] = useState('');
  const [filter, setFilter] = useState<PromptFilter>('all');
  const visible = useMemo(
    () => filterPrompts(page.stages, query, filter),
    [page.stages, query, filter],
  );

  return (
    <section
      data-testid={ids.list}
      aria-label="Built-in prompts"
      className="grid min-w-0 content-start rounded-panel border border-edge bg-panel"
    >
      <div className="flex flex-wrap items-center gap-2 p-3 pb-1">
        <input
          type="search"
          data-testid={ids.listSearch}
          aria-label="Find a prompt"
          placeholder="Find a prompt or placeholder"
          value={query}
          onChange={(event) => setQuery(event.target.value)}
          className="min-h-[var(--touch-target,32px)] min-w-0 flex-1 rounded-control border border-edge bg-canvas px-2 text-[length:var(--text-sm)] focus-visible:outline-2 focus-visible:outline-signal"
        />
        <div role="group" aria-label="Show" className="flex">
          {FILTERS.map((option, index) => (
            <button
              key={option.id}
              type="button"
              data-testid={option.testId}
              aria-pressed={filter === option.id}
              onClick={() => setFilter(option.id)}
              className={`min-h-[var(--touch-target,32px)] border border-edge px-3 text-[length:var(--text-sm)] focus-visible:outline-2 focus-visible:outline-signal ${
                index === 0 ? 'rounded-l-control' : 'rounded-r-control border-l-0'
              } ${filter === option.id ? 'bg-signal-soft font-semibold' : 'bg-panel'}`}
            >
              {option.label}
            </button>
          ))}
        </div>
      </div>
      {visible.length === 0 ? (
        <p
          data-testid={ids.listEmpty}
          className="m-0 p-3 text-[length:var(--text-sm)] text-ink-muted"
        >
          No prompt matches. Placeholders are searchable too, for example{' '}
          <code className="font-mono">task_key</code>.
        </p>
      ) : (
        visible.map((stage) => {
          const number = page.stages.findIndex((s) => s.id === stage.id) + 1;
          return (
            <div
              key={stage.id}
              id={stageAnchor(stage.id)}
              data-testid={ids.listStage}
              data-stage={stage.id}
              className="relative scroll-mt-2 py-3 pr-3 pb-1 pl-[38px] before:absolute before:top-0 before:bottom-0 before:left-5 before:w-0.5 before:bg-edge before:content-['']"
            >
              <span
                aria-hidden="true"
                className="absolute top-2.5 left-2.5 z-[1] grid size-[22px] place-items-center rounded-full border-2 border-edge bg-panel text-[length:var(--text-2xs)] font-semibold text-ink-muted"
              >
                {number}
              </span>
              <h3 className="m-0 text-[length:var(--text-sm)] font-semibold">{stage.name}</h3>
              <p
                data-testid={ids.listStageWhen}
                className="mt-px mb-1.5 text-[length:var(--text-xs)] text-ink-muted"
              >
                {stage.when}
              </p>
              {stage.prompts.map((prompt) => (
                <button
                  key={prompt.key}
                  type="button"
                  data-testid={ids.listRow}
                  data-key={prompt.key}
                  aria-current={prompt.key === selectedKey}
                  onClick={() => onSelect(prompt.key)}
                  className={`mb-[3px] grid min-h-[var(--touch-target,32px)] w-full grid-cols-[minmax(0,1fr)_auto] items-center gap-x-2 rounded-control border px-2 py-1.5 text-left text-ink hover:bg-sunken focus-visible:outline-2 focus-visible:outline-signal ${
                    prompt.key === selectedKey
                      ? 'border-signal/35 bg-signal-soft'
                      : 'border-transparent bg-transparent'
                  }`}
                >
                  <span
                    data-testid={ids.listRowName}
                    className="truncate text-[length:var(--text-sm)] font-medium"
                  >
                    {prompt.name}
                  </span>
                  <span
                    data-testid={ids.listRowChip}
                    data-status={prompt.status}
                    className="col-start-2 row-span-2 row-start-1"
                  >
                    <Chip tone={TONES[prompt.status]}>{chipLabel(prompt)}</Chip>
                  </span>
                  <span
                    data-testid={ids.listRowFile}
                    className="col-start-1 truncate font-mono text-[length:var(--text-2xs)] text-ink-muted"
                  >
                    {prompt.file}
                  </span>
                </button>
              ))}
            </div>
          );
        })
      )}
    </section>
  );
}
