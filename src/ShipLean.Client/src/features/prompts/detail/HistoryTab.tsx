import type { PromptDto } from '@shiplean/core/api/prompts';
import { promptsTestIds as ids } from '../testIds';

// Every version, newest first, with Use vN for admins, ending in the built-in the versions started from.
export function HistoryTab(props: {
  projectId: string;
  prompt: PromptDto;
  canEdit: boolean;
  onUseBuiltIn: () => void;
}) {
  // TODO: the history table from usePromptHistory
  return (
    <section
      data-testid={ids.panelHistory}
      aria-label={`History of ${props.prompt.name}`}
      data-can-edit={props.canEdit}
      data-use-built-in={typeof props.onUseBuiltIn}
    />
  );
}
