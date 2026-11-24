import type { PromptDto, PromptStageDto } from '@shiplean/core/api/prompts';

/** The text agents get now: the custom version when it is in use, else the built-in. */
export function textInUse(prompt: PromptDto): string {
  return prompt.custom?.inUse === true ? prompt.custom.text : prompt.builtInText;
}

/** Custom here means a custom version in use: status custom or built-in updated (not using built-in). */
export function isCustomised(prompt: PromptDto): boolean {
  return prompt.status === 'custom' || prompt.status === 'builtInUpdated';
}

function matches(prompt: PromptDto, needle: string): boolean {
  if (needle === '') return true;
  return [prompt.name, prompt.file, textInUse(prompt), ...prompt.placeholders].some((field) =>
    field.toLowerCase().includes(needle),
  );
}

/** The stages with only the prompts that match the search; a stage with none is left out. */
export function filterPrompts(stages: PromptStageDto[], query: string): PromptStageDto[] {
  const needle = query.trim().toLowerCase();
  return stages
    .map((stage) => ({
      ...stage,
      prompts: stage.prompts.filter((prompt) => matches(prompt, needle)),
    }))
    .filter((stage) => stage.prompts.length > 0);
}
