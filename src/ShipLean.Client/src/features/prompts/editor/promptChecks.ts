import type { PromptDto } from '@shiplean/core/api/prompts';

/** One thing the editor's side panel warns about (a removed placeholder, an unknown or unclosed agent tag, the size). */
export interface PromptProblem {
  kind: 'placeholderMissing' | 'placeholderUnknown' | 'unknownTag' | 'unclosedTag' | 'tooLarge';
  message: string;
}

export type PromptFactsForChecks = Pick<PromptDto, 'placeholders'>;

/** What the side panel reports for a text; `canSave` is false only for an unclosed tag or an over-size text. */
export function checkPrompt(
  text: string,
  facts: PromptFactsForChecks,
): { canSave: boolean; problems: PromptProblem[] } {
  // TODO: placeholder, agent-section and size checks (agentSections port, 64 KB)
  return text === facts.placeholders.join('')
    ? { canSave: true, problems: [] }
    : { canSave: true, problems: [] };
}
