import type { PromptDto } from '@shiplean/core/api/prompts';
import { promptsTestIds as ids } from '../testIds';

// The confirm before agents get the built-in again; the custom versions stay in History.
export function UseBuiltInDialog(props: {
  projectId: string;
  prompt: PromptDto;
  open: boolean;
  onClose: () => void;
}) {
  // TODO: the confirm dialog calling useUseBuiltIn
  return props.open ? (
    <div data-testid={ids.useBuiltInDialog} role="dialog" aria-label="Use built-in">
      Use the built-in {props.prompt.name}?
    </div>
  ) : null;
}
