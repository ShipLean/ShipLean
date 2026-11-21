import type { PromptDto, PromptStageDto } from '@shiplean/core/api/prompts';

export type PromptFilter = 'all' | 'custom';

/** The text agents get now: the custom version when it is in use, else the built-in. */
export function textInUse(prompt: PromptDto): string {
  return prompt.custom?.inUse === true ? prompt.custom.text : prompt.builtInText;
}

/** Custom, built-in updated and using built-in: every prompt that has a custom version behind it. */
export function isCustomised(prompt: PromptDto): boolean {
  return prompt.status !== 'builtIn';
}

function matches(prompt: PromptDto, needle: string): boolean {
  if (needle === '') return true;
  return [prompt.name, prompt.file, textInUse(prompt), ...prompt.placeholders].some((field) =>
    field.toLowerCase().includes(needle),
  );
}

/** The stages with only the prompts that pass the search and the filter; a stage with none is left out. */
export function filterPrompts(
  stages: PromptStageDto[],
  query: string,
  filter: PromptFilter,
): PromptStageDto[] {
  const needle = query.trim().toLowerCase();
  return stages
    .map((stage) => ({
      ...stage,
      prompts: stage.prompts.filter(
        (prompt) => (filter === 'all' || isCustomised(prompt)) && matches(prompt, needle),
      ),
    }))
    .filter((stage) => stage.prompts.length > 0);
}
