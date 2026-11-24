import type { PromptDto, PromptsPageDto } from '@shiplean/core/api/prompts';

export const withPrompt = (
  base: PromptsPageDto,
  key: string,
  change: (p: PromptDto) => PromptDto,
): PromptsPageDto => ({
  ...base,
  stages: base.stages.map((s) => ({
    ...s,
    prompts: s.prompts.map((p) => (p.key === key ? change(p) : p)),
  })),
});
export const guideOf = (p: PromptsPageDto) =>
  p.stages.flatMap((s) => s.prompts).find((x) => x.key === 'guide')!;
export const conflicting = (base: PromptsPageDto) =>
  withPrompt(base, 'guide', (p) => ({
    ...p,
    update: { ...p.update!, appliesCleanly: false, mergedText: '<<<<<<< mine\n' },
  }));
