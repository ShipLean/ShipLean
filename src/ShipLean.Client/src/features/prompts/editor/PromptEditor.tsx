import type { PromptDto, PromptVersionSource } from '@shiplean/core/api/prompts';
import { promptsTestIds as ids } from '../testIds';

// The Markdown editor with its side panel and footer: Save as custom vN, Cancel, the leave guard.
export function PromptEditor(props: {
  projectId: string;
  prompt: PromptDto;
  initialText: string;
  source: PromptVersionSource;
  onClose: () => void;
}) {
  // TODO: the editor, its tabs, the side panel, the note and Save
  return (
    <section
      data-testid={ids.editor}
      aria-label={`Edit ${props.prompt.name}`}
      data-source={props.source}
    >
      Editing {props.prompt.name}
    </section>
  );
}
