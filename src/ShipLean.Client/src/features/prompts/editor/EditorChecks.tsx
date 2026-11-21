import type { PromptDto } from '@shiplean/core/api/prompts';
import { promptsTestIds as ids } from '../testIds';

// The editor's side panel: placeholders with Put back, agent-specific sections with Add and Did you mean, the size meter.
export function EditorChecks(props: {
  text: string;
  prompt: PromptDto;
  onChange: (text: string) => void;
}) {
  // TODO: the checks from checkPrompt, with Put back, the add and fix buttons and the size meter
  return (
    <aside
      data-testid={ids.checks}
      aria-label={`Checks for ${props.prompt.name}`}
      data-length={props.text.length}
    />
  );
}
