import type { PromptStage, PromptStageDto } from '@shiplean/core/api/prompts';
import { promptsTestIds as ids } from '../testIds';
import { isCustomised } from '../header/filterPrompts';

// Under 900px the strip becomes this select (D7): '4 · Build · 3 prompts, 1 custom'. Choosing a stage opens its first prompt.
export function StageSelect({
  stages,
  selected,
  onSelect,
}: {
  stages: PromptStageDto[];
  selected: PromptStage | null;
  onSelect: (stage: PromptStage) => void;
}) {
  return (
    <select
      data-testid={ids.stageSelect}
      aria-label="Stage"
      hidden={stages.length === 0}
      value={selected ?? ''}
      onChange={(event) => onSelect(event.target.value as PromptStage)}
      className="field hidden h-9 w-full touch:block"
    >
      {stages.map((stage, index) => {
        const count = stage.prompts.length;
        const customs = stage.prompts.filter(isCustomised).length;
        return (
          <option key={stage.id} value={stage.id}>
            {`${index + 1} · ${stage.name} · ${count} ${count === 1 ? 'prompt' : 'prompts'}${
              customs > 0 ? `, ${customs} custom` : ''
            }`}
          </option>
        );
      })}
    </select>
  );
}
