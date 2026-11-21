import type { PromptStage, PromptStageDto } from '@shiplean/core/api/prompts';
import { promptsTestIds as ids } from '../testIds';

// The seven stages across the top, each with its prompt and custom counts; a click jumps the list to it.
export function StageStrip({
  stages,
}: {
  stages: PromptStageDto[];
  selected: PromptStage | null;
  onSelect: (stage: PromptStage) => void;
}) {
  // TODO: the stage buttons with their counts, jumping the list to the stage
  return (
    <nav data-testid={ids.stageStrip} aria-label="Workflow stages" hidden={stages.length === 0} />
  );
}
