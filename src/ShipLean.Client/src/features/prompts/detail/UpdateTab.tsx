import type { PromptDto } from '@shiplean/core/api/prompts';
import { promptsTestIds as ids } from '../testIds';

// The built-in's own change, with Applies cleanly and the apply actions.
export function UpdateTab(props: {
  projectId: string;
  prompt: PromptDto;
  canEdit: boolean;
  onApplyAndEdit: () => void;
}) {
  // TODO: the built-in diff and the update actions
  return (
    <section
      data-testid={ids.panelUpdate}
      aria-label={`Built-in update of ${props.prompt.name}`}
      data-can-edit={props.canEdit}
    />
  );
}
