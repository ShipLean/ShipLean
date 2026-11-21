import type { PromptDto } from '@shiplean/core/api/prompts';
import { promptsTestIds as ids } from '../testIds';

// The diff of the custom version against the built-in it started from.
export function YourChangesTab(props: { prompt: PromptDto }) {
  // TODO: the diff view of prompt.yourChangesPatch, parsed with diffParse
  return (
    <section
      data-testid={ids.panelYourChanges}
      aria-label={`Your changes to ${props.prompt.name}`}
    />
  );
}
