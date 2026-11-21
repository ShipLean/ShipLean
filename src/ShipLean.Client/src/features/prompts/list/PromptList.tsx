import type { PromptKey, PromptsPageDto } from '@shiplean/core/api/prompts';
import { promptsTestIds as ids } from '../testIds';

// The prompts grouped by stage, with search and the All / Custom filter; selecting one calls onSelect.
export function PromptList({
  page,
  onSelect,
}: {
  page: PromptsPageDto;
  selectedKey: PromptKey | null;
  onSelect: (key: PromptKey) => void;
}) {
  // TODO: the stage rail, the rows with their chips, the search and the All / Custom filter
  return (
    <section
      data-testid={ids.list}
      aria-label="Built-in prompts"
      className="grid content-start gap-1"
    >
      {page.stages
        .flatMap((stage) => stage.prompts)
        .map((prompt) => (
          <button
            key={prompt.key}
            type="button"
            data-testid={ids.listRow}
            onClick={() => onSelect(prompt.key)}
            className="text-left"
          >
            {prompt.name}
          </button>
        ))}
    </section>
  );
}
