import type { PromptDto } from '@shiplean/core/api/prompts';
import { checkSections } from './agentSections';
import { missingPlaceholders, unknownPlaceholders } from './placeholders';

/** One thing the editor's side panel warns about (a removed or unknown placeholder, an agent-tag problem, the size). */
export interface PromptProblem {
  kind:
    | 'placeholderMissing'
    | 'placeholderUnknown'
    | 'unknownTag'
    | 'unclosed'
    | 'nested'
    | 'strayClose'
    | 'tooLarge';
  message: string;
  /** 1-based line of a tag problem. */
  line?: number;
  /** The tag name a tag problem is about. */
  tag?: string;
  /** The placeholder a placeholder problem is about. */
  placeholder?: string;
  /** The agent id an unknown tag probably meant. */
  suggestion?: string;
}

export type PromptFactsForChecks = Pick<PromptDto, 'placeholders'>;

const BLOCKING = new Set<PromptProblem['kind']>(['unclosed', 'nested', 'strayClose']);

function tagMessage(
  kind: 'unclosed' | 'nested' | 'strayClose' | 'unknownTag',
  tag: string,
): string {
  switch (kind) {
    case 'unclosed':
      return `<${tag}> isn't closed with </${tag}>.`;
    case 'nested':
      return `<${tag}> starts inside another section.`;
    case 'strayClose':
      return `</${tag}> closes a section that isn't open.`;
    default:
      return `<${tag}> isn't an agent ShipLean recognises, so every agent gets it as typed.`;
  }
}

/** What the side panel reports for a text; section errors block Save (D20), placeholders and unknown tags only warn (D6). */
export function checkPrompt(
  text: string,
  facts: PromptFactsForChecks,
): { canSave: boolean; problems: PromptProblem[] } {
  const problems: PromptProblem[] = [
    ...missingPlaceholders(text, facts.placeholders).map((placeholder): PromptProblem => ({
      kind: 'placeholderMissing',
      placeholder,
      message: `${placeholder} is missing, so agents won't get that part.`,
    })),
    ...unknownPlaceholders(text, facts.placeholders).map((placeholder): PromptProblem => ({
      kind: 'placeholderUnknown',
      placeholder,
      message: `${placeholder} isn't one ShipLean fills in for this prompt; it stays as typed.`,
    })),
    ...checkSections(text).map((problem): PromptProblem => ({
      kind: problem.kind,
      line: problem.line,
      tag: problem.tag,
      ...(problem.suggestion === null ? {} : { suggestion: problem.suggestion }),
      message: tagMessage(problem.kind, problem.tag),
    })),
  ];
  return { canSave: !problems.some((problem) => BLOCKING.has(problem.kind)), problems };
}
